using Microsoft.Graphics.Canvas.Brushes;
using Microsoft.Graphics.Canvas.Text;
using Microsoft.Graphics.Canvas.UI.Xaml;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Display;
using Windows.System;
using Windows.UI;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Text.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

//https://go.microsoft.com/fwlink/?LinkId=234236 上介绍了“用户控件”项模板

namespace GaoToolkit
{
    public sealed partial class FreeTextBox : UserControl, INotifyPropertyChanged, IDisposable
    {
        CanvasTextLayout _textLayout;
        CanvasTextFormat _textFormat;
        bool _isBold = false;
        Size _canvasRenderSize;
        bool _hasUnderline = false;
        float _canvasFontSize = 24;
        float _canvasCharacterSpacing;
        float _canvasLineSpacing;
        Color _canvasTextForeground = Colors.Black;
        FontStyle _canvasFontStyle = FontStyle.Normal;
        string _canvasFontFamily = FontFamily.XamlAutoFontFamily.Source;
        CanvasVerticalAlignment _canvasVerticalAlignment = CanvasVerticalAlignment.Top;
        CanvasTextDirection _textDirection = CanvasTextDirection.LeftToRightThenTopToBottom;
        CanvasHorizontalAlignment _canvasHorizontalAlignment = CanvasHorizontalAlignment.Left;
        float _canvasTextOpacity = 1;
        bool _isSelfAdaption = false;
        Vector2 _caretPosition = new Vector2(0, 0);
        Vector2 _caretEndPosition = new Vector2(0, 0);
        float _drawOffsetX = 0;
        float _drawOffsetY = 0;
        Matrix3x2 _uiAdapt = Matrix3x2.Identity;
        Matrix3x2 _uiAdaptInvert = Matrix3x2.Identity;

        //输入法功能
        CoreTextEditContext _editContext;
        string _text = string.Empty;
        CoreTextRange _selection;
        bool _internalFocus = false;
        InputPane _inputPane;
        CoreWindow _coreWindow;
        DispatcherTimer _caretTimer;
        bool _drawCaret = false;
        //向左选中
        bool _extendingLeft = false;
        //快捷键
        bool _canUseCtrlC = true;
        bool _canUseCtrlV = true;
        bool _canUseCtrlX = true;

        #region Property

        public string Text
        {
            get
            {
                return _text;
            }
            set
            {
                _text = value;
                if (string.IsNullOrEmpty(_text))
                {
                    _editContext?.NotifyTextChanged(
                        _selection,
                        _text.Length,
                        new CoreTextRange()
                        {
                            StartCaretPosition = _text.Length,
                            EndCaretPosition = _text.Length
                        });
                }
            }
        }

        public bool IsBold
        {
            get
            {
                return _isBold;
            }
            set
            {
                _isBold = value;
                RaiseProperty("IsBold");
            }
        }

        public FontStyle CanvasFontStyle
        {
            get
            {
                return _canvasFontStyle;
            }
            set
            {
                _canvasFontStyle = value;
                RaiseProperty("FontStyle");
            }
        }

        public float CanvasFontSize
        {
            get
            {
                return _canvasFontSize;
            }
            set
            {
                _canvasFontSize = value < 1f ? 1f : value;
                RaiseProperty("FontSize");
            }
        }

        public CanvasHorizontalAlignment CanvasHorizontalAlignment
        {
            get
            {
                return _canvasHorizontalAlignment;
            }
            set
            {
                _canvasHorizontalAlignment = value;
                RaiseProperty("CanvasHorizontalAlignment");
            }
        }

        public CanvasVerticalAlignment CanvasVerticalAlignment
        {
            get
            {
                return _canvasVerticalAlignment;
            }
            set
            {
                _canvasVerticalAlignment = value;
                RaiseProperty("CanvasVerticalAlignment");
            }
        }

        public string CanvasFontFamily
        {
            get
            {
                return _canvasFontFamily;
            }
            set
            {
                _canvasFontFamily = value;
                RaiseProperty("CanvasFontFamily");
            }
        }

        public Color CanvasTextForeground
        {
            get => _canvasTextForeground;
            set
            {
                _canvasTextForeground = value;
                RaiseProperty("CanvasTextForeground");
            }
        }

        public bool IsTextHorizontal
        {
            get
            {
                return _textDirection == CanvasTextDirection.LeftToRightThenTopToBottom;
            }
            set
            {
                _textDirection =
                    value ? CanvasTextDirection.LeftToRightThenTopToBottom
                    : CanvasTextDirection.TopToBottomThenRightToLeft;
                UpdateTextUI();
            }
        }

        public bool HasUnderline
        {
            get
            {
                return _hasUnderline;
            }
            set
            {
                _hasUnderline = value;
                RaiseProperty("HasUnderline");
            }
        }

        public float CanvasCharacterSpacing
        {
            get => _canvasCharacterSpacing;
            set
            {
                _canvasCharacterSpacing = value;
                RaiseProperty("CanvasCharacterSpacing");
            }
        }

        public float CanvasLineSpacing
        {
            get => _canvasLineSpacing;
            set
            {
                _canvasLineSpacing = value;
                RaiseProperty("CanvasLineSpacing");
            }
        }

        public Size CanvasRenderSize
        {
            get
            {
                return _canvasRenderSize;
            }
            set
            {
                _canvasRenderSize = value;
                RaiseProperty("CanvasRenderSize");
            }
        }



        public Matrix3x2 UIAdapt
        {
            get
            {
                return _uiAdapt;
            }
            set
            {
                _uiAdapt = value;
                Matrix3x2.Invert(_uiAdapt, out _uiAdaptInvert);
            }
        }

        public bool NeedSelfAdaption { get; set; }

        #endregion

        public event PropertyChangedEventHandler PropertyChanged;

        public FreeTextBox()
        {
            this.InitializeComponent();
            _caretTimer = new DispatcherTimer();
            _caretTimer.Interval = TimeSpan.FromSeconds(0.5);
            _caretTimer.Tick += (s, e) =>
            {
                if (_internalFocus && !HasSelection())
                {
                    _drawCaret = !_drawCaret;
                    UpdateTextUI();
                }
            };

            PointerEntered += (s, e) =>
            {
                Window.Current.CoreWindow.PointerCursor
                    = new CoreCursor(CoreCursorType.IBeam, 0);
            };

            PointerExited += (s, e) =>
            {
                Window.Current.CoreWindow.PointerCursor
                    = new CoreCursor(CoreCursorType.Arrow, 0);
            };


            _coreWindow = CoreWindow.GetForCurrentThread();
            _coreWindow.KeyDown += CoreWindow_KeyDown;
            _coreWindow.KeyUp += CoreWindow_KeyUp;


            //不同的方法获取焦点
            _coreWindow.PointerPressed += CoreWindow_PointerPressed;
            //DoubleTapped += FreeTextBox_DoubleTapped;

            CoreTextServicesManager manager = CoreTextServicesManager.GetForCurrentView();
            _editContext = manager.CreateEditContext();
            _inputPane = InputPane.GetForCurrentView();
            _editContext.InputPaneDisplayPolicy = CoreTextInputPaneDisplayPolicy.Automatic;
            _editContext.InputScope = CoreTextInputScope.Text;
            _editContext.TextRequested += EditContext_TextRequested;
            _editContext.SelectionRequested += EditContext_SelectionRequested;
            _editContext.FocusRemoved += EditContext_FocusRemoved;
            _editContext.TextUpdating += EditContext_TextUpdating;
            _editContext.SelectionUpdating += EditContext_SelectionUpdating;
            _editContext.LayoutRequested += EditContext_LayoutRequested;
            //以下三个事件需注册即使为空
            _editContext.FormatUpdating += EditContext_FormatUpdating;
            _editContext.CompositionStarted += EditContext_CompositionStarted;
            _editContext.CompositionCompleted += EditContext_CompositionCompleted;

        }



        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }

        private void UserControl_Unloaded(object sender, RoutedEventArgs e)
        {

        }

        private void UserControl_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (Visibility == Visibility.Collapsed)
            {
                return;
            }
            _editContext?.NotifyLayoutChanged();
            //UpdateTextUI();
        }

        private void Canvas_Draw(CanvasControl sender, CanvasDrawEventArgs args)
        {
            if (string.IsNullOrEmpty(_text)
                || _isSelfAdaption)
            {
                return;
            }

            args.DrawingSession.Transform = _uiAdapt;

            float renderWidth = (float)_canvasRenderSize.Width;
            float renderHeight = (float)_canvasRenderSize.Height;
            if (_textLayout != null)
                _textLayout.Dispose();
            if (_textFormat != null)
            {
                _textFormat.Dispose();
            }
            _textFormat = new CanvasTextFormat()
            {
                Direction = _textDirection,
                FontSize = _canvasFontSize,
                FontFamily = _canvasFontFamily,
                FontStyle = _canvasFontStyle,
                HorizontalAlignment = _canvasHorizontalAlignment,
                VerticalAlignment = _canvasVerticalAlignment,

                FontWeight = new FontWeight()
                {
                    Weight = IsBold ? (ushort)700 : (ushort)400
                }
            };
            if (_canvasLineSpacing == 0)
            {
                _textFormat.LineSpacing = -1;
                _textFormat.LineSpacingBaseline = 1;
            }
            else
            {
                _textFormat.LineSpacing = _canvasLineSpacing * _textFormat.FontSize;
                _textFormat.LineSpacingBaseline = (float)(_textFormat.LineSpacing * 0.8);
            }

            _textLayout = new CanvasTextLayout(sender, _text, _textFormat, renderWidth, renderHeight);

            _textLayout.SetUnderline(0, Text.Length, HasUnderline);

            _textLayout.SetCharacterSpacing(0, Text.Length, 0, _canvasFontSize / 10 * CharacterSpacing, 0);

            if (_canvasLineSpacing == 0)
            {
                _textLayout.LineSpacing = -1;
                _textLayout.LineSpacingBaseline = 1;
            }
            else
            {
                _textLayout.LineSpacing = _canvasLineSpacing * _textFormat.FontSize;
                _textLayout.LineSpacingBaseline = (float)(_textLayout.LineSpacing * 0.8);
            }

            //自适应文字布局
            if (NeedSelfAdaption)
            {
                _isSelfAdaption = true;
                if (_isSelfAdaption)
                {
                    if (IsTextHorizontal)
                    {
                        Height = (int)(_textLayout.LayoutBounds.Height + 0.5);
                    }
                    else
                    {
                        Width = (int)(_textLayout.LayoutBounds.Width + 0.5);
                    }
                }
                _isSelfAdaption = false;
            }
            _drawOffsetX = -(float)_textLayout.DrawBounds.X;
            _drawOffsetY = -(float)_textLayout.DrawBounds.Y;

            _textLayout.SetBrush(0, _text.Length, null);
            if (HasSelection() && _internalFocus)
            {
                //_selection
                int firstIndex = Math.Min(_selection.StartCaretPosition, _selection.EndCaretPosition);
                int length = Math.Abs(_selection.EndCaretPosition - _selection.StartCaretPosition);
                CanvasTextLayoutRegion[] descriptions = _textLayout.GetCharacterRegions(firstIndex, length);
                foreach (CanvasTextLayoutRegion description in descriptions)
                {
                    args.DrawingSession.FillRectangle(InflateRect(description.LayoutBounds), Colors.Blue);
                }
                var selectionSolidBrush = new CanvasSolidColorBrush(sender, Colors.White);
                _textLayout.SetBrush(firstIndex, length, selectionSolidBrush);
            }

            //绘制文本
            using (args.DrawingSession.CreateLayer(_canvasTextOpacity))
            {
                args.DrawingSession.DrawTextLayout(_textLayout, _drawOffsetX, _drawOffsetY, _canvasTextForeground);
            }


            //绘制光标
            if (_drawCaret && _internalFocus)
            {
                if (_selection.StartCaretPosition == _selection.EndCaretPosition)
                {
                    CanvasTextLayoutRegion textLayoutRegion;
                    if (IsTextHorizontal)
                    {
                        if (_selection.EndCaretPosition == 0)
                        {
                            _caretPosition = _textLayout.GetCaretPosition(0, false, out textLayoutRegion);
                            _caretEndPosition = new Vector2(_caretPosition.X, _caretPosition.Y + (float)textLayoutRegion.LayoutBounds.Height);
                        }
                        else
                        {
                            _caretPosition = _textLayout.GetCaretPosition(_selection.EndCaretPosition - 1, true, out textLayoutRegion);
                            var substr = _text.Substring(_selection.EndCaretPosition - 1, 1);
                            if (substr == "\r")
                            {
                                _caretPosition = new Vector2(0, _caretPosition.Y + (float)textLayoutRegion.LayoutBounds.Height);
                            }
                            _caretEndPosition = new Vector2(_caretPosition.X, _caretPosition.Y + (float)textLayoutRegion.LayoutBounds.Height);
                        }
                    }
                    else
                    {
                        //竖版坐标起始在右上角
                        if (_selection.EndCaretPosition == 0)
                        {
                            _caretPosition = _textLayout.GetCaretPosition(0, false, out textLayoutRegion);
                            _caretEndPosition = new Vector2(_caretPosition.X - (float)textLayoutRegion.LayoutBounds.Width, _caretPosition.Y);
                        }
                        else
                        {
                            _caretPosition = _textLayout.GetCaretPosition(_selection.EndCaretPosition - 1, true, out textLayoutRegion);
                            var substr = _text.Substring(_selection.EndCaretPosition - 1, 1);
                            if (substr == "\r")
                            {
                                _caretPosition = new Vector2(_caretPosition.X - (float)textLayoutRegion.LayoutBounds.Width, 0);
                            }
                            _caretEndPosition = new Vector2(_caretPosition.X - (float)textLayoutRegion.LayoutBounds.Width, _caretPosition.Y);
                        }
                    }
                }
                args.DrawingSession.DrawLine(
                    new Vector2(_caretPosition.X + _drawOffsetX, _caretPosition.Y + _drawOffsetY),
                    new Vector2(_caretEndPosition.X + _drawOffsetX, _caretEndPosition.Y + _drawOffsetY),
                    Colors.Black);
            }


        }

        private void Canvas_CreateResources(CanvasControl sender, Microsoft.Graphics.Canvas.UI.CanvasCreateResourcesEventArgs args)
        {
            //_textFormat = new CanvasTextFormat
            //{
            //    FontSize = 24,
            //    WordWrapping = CanvasWordWrapping.Character,
            //    FontWeight = new FontWeight //_isBold ? (ushort)700 :
            //    {
            //        Weight = 400
            //    },
            //    FontStyle = FontStyle.Normal,
            //    FontStretch = FontStretch.Normal,
            //    HorizontalAlignment = CanvasHorizontalAlignment.Left,
            //    VerticalAlignment = CanvasVerticalAlignment.Top,
            //    FontFamily = FontFamily.XamlAutoFontFamily.Source
            //};
        }

        private void Canvas_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            CoreTextRange range = _selection;
            range.StartCaretPosition = GetHitIndex(e.GetCurrentPoint(MyWin2dCanvas).Position);
            range.EndCaretPosition = range.StartCaretPosition;
            SetSelectionAndNotify(range);

            UpdateTextUI();
        }

        private void Canvas_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            bool needDraw = false;
            CoreTextRange range = _selection;
            foreach (var point in e.GetIntermediatePoints(MyWin2dCanvas))
            {
                if (point.IsInContact)
                {
                    var selectionEndIndex = GetHitIndex(point.Position);
                    int d = 0;
                    if (!_extendingLeft)
                    {
                        d = selectionEndIndex - range.EndCaretPosition;
                    }
                    else
                    {
                        d = selectionEndIndex - range.StartCaretPosition;
                    }

                    if (d != 0)
                    {
                        if (!HasSelection())
                        {
                            _extendingLeft = d < 0;
                        }

                        if (_extendingLeft)
                        {
                            var start = Math.Max(0, selectionEndIndex);
                            if (range.StartCaretPosition != start)
                            {
                                range.StartCaretPosition = start;
                                needDraw = true;
                            }
                        }
                        else
                        {
                            int end = Math.Min(_text.Length, selectionEndIndex);
                            if (range.EndCaretPosition != end)
                            {
                                range.EndCaretPosition = end;
                                needDraw = true;
                            }
                        }

                        if (range.EndCaretPosition < range.StartCaretPosition)
                        {
                            _extendingLeft = true;
                            var end = range.StartCaretPosition;
                            range.StartCaretPosition = range.EndCaretPosition;
                            range.EndCaretPosition = end;
                        }

                        if (needDraw)
                        {
                            SetSelectionAndNotify(range);
                        }
                    }
                }
            }
            e.Handled = true;
        }

        private void FreeTextBox_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            //Rect contentRect = GetElementRect(this);
            //bool isContains = contentRect.Contains(e.GetPosition(Window.Current.Content));
            //if (_internalFocus && !isContains)
            //{
            //    RemoveInternalFocus();
            //}
            //else if (isContains)
            //{
            //    SetInternalFocus();

            //}
            //e.Handled = true;
        }

        //设置焦点
        private void CoreWindow_PointerPressed(CoreWindow sender, PointerEventArgs args)
        {
            Rect contentRect = GetElementRect(this);
            bool isContains = contentRect.Contains(args.CurrentPoint.Position);
            if (_internalFocus
                && !isContains)
            {
                RemoveInternalFocus();
            }
            else if (isContains)
            {
                SetInternalFocus();
            }
        }


        //设置焦点时会循环调用, 可作为光标绘制入口
        private void EditContext_LayoutRequested(CoreTextEditContext sender, CoreTextLayoutRequestedEventArgs args)
        {
            CoreTextLayoutRequest request = args.Request;
            Rect contentRect = GetElementRect(this);

            Rect windowBounds = Window.Current.CoreWindow.Bounds;
            contentRect.X += windowBounds.X;
            contentRect.Y += windowBounds.Y;

            double scaleFactor = DisplayInformation.GetForCurrentView().RawPixelsPerViewPixel;
            contentRect = ScaleRect(contentRect, scaleFactor);
            request.LayoutBounds.TextBounds = contentRect; //selectionRect;
            request.LayoutBounds.ControlBounds = contentRect;
        }

        //在文本输入服务器需要从文本输入控件获取文本范围时发生。
        private void EditContext_TextRequested(CoreTextEditContext sender, CoreTextTextRequestedEventArgs args)
        {
            CoreTextTextRequest request = args.Request;
            request.Text = _text.Substring(
                request.Range.StartCaretPosition,
                Math.Min(request.Range.EndCaretPosition, _text.Length) - request.Range.StartCaretPosition);
        }

        //在文本输入服务器需要获取表示当前在文本输入控件中 "选择的文本" 的文本范围时发生。
        private void EditContext_SelectionRequested(CoreTextEditContext sender, CoreTextSelectionRequestedEventArgs args)
        {
            CoreTextSelectionRequest request = args.Request;
            request.Selection = _selection;
        }


        private void EditContext_SelectionUpdating(CoreTextEditContext sender, CoreTextSelectionUpdatingEventArgs args)
        {
            CoreTextRange range = args.Selection;
            SetSelection(range);
        }

        private void EditContext_TextUpdating(CoreTextEditContext sender, CoreTextTextUpdatingEventArgs args)
        {
            CoreTextRange range = args.Range;
            string newText = args.Text;
            CoreTextRange newSelection = args.NewSelection;

            _text = _text.Substring(0, range.StartCaretPosition) +
                newText +
                _text.Substring(Math.Min(_text.Length, range.EndCaretPosition));
            newSelection.EndCaretPosition = newSelection.StartCaretPosition;
            SetSelection(newSelection);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
        }


        private void CoreWindow_KeyUp(CoreWindow sender, KeyEventArgs args)
        {
            switch (args.VirtualKey)
            {
                case VirtualKey.C:
                    _canUseCtrlC = true;
                    break;
                case VirtualKey.V:
                    _canUseCtrlV = true;
                    break;
                case VirtualKey.X:
                    _canUseCtrlX = true;
                    break;
            }
        }

        private void CoreWindow_KeyDown(CoreWindow sender, KeyEventArgs args)
        {
            if (!_internalFocus)
            {
                return;
            }
            CoreTextRange range = _selection;
            switch (args.VirtualKey)
            {
                // Backspace
                case VirtualKey.Back:
                    if (HasSelection())
                    {
                        ReplaceText(range, "");
                    }
                    else
                    {
                        range.StartCaretPosition = Math.Max(0, range.StartCaretPosition - 1);
                        ReplaceText(range, "");
                    }
                    break;
                case VirtualKey.Left:
                    KeyLeftPressed(range);
                    break;
                case VirtualKey.Right:
                    KeyRightPressed(range);
                    break;
                case VirtualKey.Up:
                    KeyUpPressed(range);
                    break;
                case VirtualKey.Down:
                    KeyDownPressed(range);
                    break;
                case VirtualKey.Enter:
                    if (!HasSelection())
                    {
                        if (range.StartCaretPosition >= _text.Length)
                        {
                            _text += "\r";
                        }
                        else
                        {
                            _text = _text.Insert(range.StartCaretPosition, "\r");
                        }
                        _extendingLeft = false;
                        range.StartCaretPosition = Math.Min(_text.Length, range.StartCaretPosition + 1);
                        range.EndCaretPosition = range.StartCaretPosition;
                        SetSelectionAndNotify(range);
                        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
                    }
                    break;
                case VirtualKey.C:
                    if (!_canUseCtrlC)
                    {
                        return;
                    }
                    _canUseCtrlC = false;
                    if (_coreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        KeyCtrlCPressed(range);
                    }
                    break;
                case VirtualKey.V:
                    if (!_canUseCtrlV)
                    {
                        return;
                    }
                    _canUseCtrlV = false;
                    if (_coreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        KeyCtrlVPressed(range);
                    }
                    break;
                case VirtualKey.X:
                    if (!_canUseCtrlX)
                    {
                        return;
                    }
                    _canUseCtrlX = false;
                    if (_coreWindow.GetKeyState(VirtualKey.Control).HasFlag(CoreVirtualKeyStates.Down))
                    {
                        KeyCtrlXPressed(range);
                    }
                    break;
            }
        }

        private void EditContext_FocusRemoved(CoreTextEditContext sender, object args)
        {
            RemoveInternalFocusWorker();
        }

        private void EditContext_CompositionCompleted(CoreTextEditContext sender, CoreTextCompositionCompletedEventArgs args)
        {

        }

        private void EditContext_CompositionStarted(CoreTextEditContext sender, CoreTextCompositionStartedEventArgs args)
        {

        }

        private void EditContext_FormatUpdating(CoreTextEditContext sender, CoreTextFormatUpdatingEventArgs args)
        {

        }

        void KeyLeftPressed(CoreTextRange range)
        {
            if (IsTextHorizontal)
            {
                if (_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (!HasSelection())
                    {
                        _extendingLeft = true;
                    }
                    AdjustSelectionEndpoint(-1);
                }
                else
                {
                    if (HasSelection())
                    {
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    else
                    {
                        range.StartCaretPosition = Math.Max(0, range.StartCaretPosition - 1);
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    SetSelectionAndNotify(range);
                }
            }
            else
            {
                range.EndCaretPosition = GetHitIndex(
                    new Point(
                        _caretEndPosition.X - 1.5,
                        _caretEndPosition.Y));
                if (!_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    range.StartCaretPosition = range.EndCaretPosition;
                }
                SetSelectionAndNotify(range);
            }
        }

        void KeyRightPressed(CoreTextRange range)
        {
            if (IsTextHorizontal)
            {
                if (_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (!HasSelection())
                    {
                        _extendingLeft = false;
                    }
                    AdjustSelectionEndpoint(+1);
                }
                else
                {
                    if (HasSelection())
                    {
                        range.StartCaretPosition = range.EndCaretPosition;
                    }
                    else
                    {
                        range.StartCaretPosition = Math.Min(_text.Length, range.StartCaretPosition + 1);
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    SetSelectionAndNotify(range);
                }
            }
            else
            {
                range.StartCaretPosition = GetHitIndex(
                    new Point(
                        _caretPosition.X + 1.5,
                        _caretPosition.Y));
                if (!_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    range.EndCaretPosition = range.StartCaretPosition;
                }
                SetSelectionAndNotify(range);
            }
        }

        void KeyUpPressed(CoreTextRange range)
        {
            if (IsTextHorizontal)
            {
                range.StartCaretPosition = GetHitIndex(
                    new Point(
                        _caretPosition.X - 1,
                        _caretPosition.Y - Math.Abs(_caretEndPosition.Y - _caretPosition.Y) / 2));
                if (!_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    range.EndCaretPosition = range.StartCaretPosition;
                }
                SetSelectionAndNotify(range);
            }
            else
            {
                if (_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (!HasSelection())
                    {
                        _extendingLeft = true;
                    }
                    AdjustSelectionEndpoint(-1);
                }
                else
                {
                    if (HasSelection())
                    {
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    else
                    {
                        range.StartCaretPosition = Math.Max(0, range.StartCaretPosition - 1);
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    SetSelectionAndNotify(range);
                }
            }
        }

        void KeyDownPressed(CoreTextRange range)
        {
            if (IsTextHorizontal)
            {
                range.EndCaretPosition = GetHitIndex(
                    new Point(
                        _caretEndPosition.X - 1,
                        _caretEndPosition.Y + Math.Abs(_caretEndPosition.Y - _caretPosition.Y) / 2));
                if (!_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    range.StartCaretPosition = range.EndCaretPosition;
                }
                SetSelectionAndNotify(range);
            }
            else
            {
                if (_coreWindow.GetKeyState(VirtualKey.Shift).HasFlag(CoreVirtualKeyStates.Down))
                {
                    if (!HasSelection())
                    {
                        _extendingLeft = false;
                    }
                    AdjustSelectionEndpoint(+1);
                }
                else
                {
                    if (HasSelection())
                    {
                        range.StartCaretPosition = range.EndCaretPosition;
                    }
                    else
                    {
                        range.StartCaretPosition = Math.Min(_text.Length, range.StartCaretPosition + 1);
                        range.EndCaretPosition = range.StartCaretPosition;
                    }
                    SetSelectionAndNotify(range);
                }
            }
        }

        void KeyCtrlCPressed(CoreTextRange range)
        {
            Clipboard.Clear();
            if (HasSelection())
            {
                var dp = new DataPackage();
                dp.SetText(
                    _text.Substring(
                        range.StartCaretPosition,
                        range.EndCaretPosition - range.StartCaretPosition));
                Clipboard.SetContent(dp);
            }
        }

        async void KeyCtrlVPressed(CoreTextRange range)
        {
            var vstr = await Clipboard.GetContent()?.GetTextAsync();
            ReplaceText(range, vstr ?? "");
        }

        void KeyCtrlXPressed(CoreTextRange range)
        {
            Clipboard.Clear();
            if (HasSelection())
            {
                var dp = new DataPackage();
                dp.SetText(
                    _text.Substring(
                        range.StartCaretPosition,
                        range.EndCaretPosition - range.StartCaretPosition));
                Clipboard.SetContent(dp);
                ReplaceText(range, "");
            }
        }


        private int GetHitIndex(Point mouseOverPt)
        {
            var tans = Vector2.Transform(mouseOverPt.ToVector2(), _uiAdaptInvert);
            CanvasTextLayoutRegion textLayoutRegion;
            bool isTrailing = true;
            var hasHit = _textLayout.HitTest(
                tans.X,
                tans.Y,
                out textLayoutRegion,
                out isTrailing);
            return isTrailing ? textLayoutRegion.CharacterIndex + 1 : textLayoutRegion.CharacterIndex;
        }


        void UpdateTextUI()
        {
            MyWin2dCanvas.Invalidate();
        }

        //设置焦点
        public void SetInternalFocus()
        {
            if (!_internalFocus)
            {
                _internalFocus = true;
                Focus(FocusState.Programmatic);
                UpdateTextUI();
                _editContext.NotifyFocusEnter();
            }
            _caretTimer.Start();
            _inputPane.TryShow();
        }

        //取消焦点
        public void RemoveInternalFocus()
        {
            if (_internalFocus)
            {
                _editContext.NotifyFocusLeave();
                RemoveInternalFocusWorker();
            }
        }

        void RemoveInternalFocusWorker()
        {
            _internalFocus = false;
            _drawCaret = false;
            _inputPane.TryHide();
            UpdateTextUI();
            _caretTimer.Stop();
        }

        bool HasSelection()
        {
            return _selection.StartCaretPosition != _selection.EndCaretPosition;
        }

        static Rect GetElementRect(FrameworkElement element)
        {
            GeneralTransform transform = element.TransformToVisual(null);
            Point point = transform.TransformPoint(new Point());
            return new Rect(point, new Size(element.ActualWidth, element.ActualHeight));
        }

        void AdjustSelectionEndpoint(int direction)
        {
            CoreTextRange range = _selection;
            if (_extendingLeft)
            {
                range.StartCaretPosition = Math.Max(0, range.StartCaretPosition + direction);
            }
            else
            {
                range.EndCaretPosition = Math.Min(_text.Length, range.EndCaretPosition + direction);
            }
            SetSelectionAndNotify(range);
        }


        void SetSelectionAndNotify(CoreTextRange selection)
        {
            SetSelection(selection);
            _editContext.NotifySelectionChanged(_selection);
        }

        void SetSelection(CoreTextRange selection)
        {
            _caretTimer.Stop();
            _selection = selection;
            UpdateTextUI();
            _drawCaret = true;
            _caretTimer.Start();
        }

        void ReplaceText(CoreTextRange modifiedRange, string text)
        {
            _text = _text.Substring(0, modifiedRange.StartCaretPosition) +
              text +
              _text.Substring(modifiedRange.EndCaretPosition);

            _selection.StartCaretPosition = modifiedRange.StartCaretPosition + text.Length;
            _selection.EndCaretPosition = _selection.StartCaretPosition;

            SetSelection(_selection);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Text"));
            _editContext.NotifyTextChanged(modifiedRange, text.Length, _selection);
        }

        static Rect ScaleRect(Rect rect, double scale)
        {
            rect.X *= scale;
            rect.Y *= scale;
            rect.Width *= scale;
            rect.Height *= scale;
            return rect;
        }

        Rect InflateRect(Rect r)
        {
            return new Rect(
                new Point(Math.Floor(r.Left), Math.Floor(r.Top)),
                new Point(Math.Ceiling(r.Right), Math.Ceiling(r.Bottom)));
        }


        void RaiseProperty(string property)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(property));
            UpdateTextUI();
        }

        public void Dispose()
        {
            _caretTimer?.Stop();
            _caretTimer = null;
            MyWin2dCanvas.RemoveFromVisualTree();
            MyWin2dCanvas = null;


        }


    }
}
