﻿using Sandbox.Common;
using Sandbox.Common.ObjectBuilders.Gui;
using System;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows.Forms;
using VRage;
using VRage.Input;
using VRage.Library.Utils;
using VRage.Utils;
using VRageMath;

namespace Sandbox.Graphics.GUI
{
    public delegate void LinkClicked(MyGuiControlBase sender, string url);

    [MyGuiControlType(typeof(MyObjectBuilder_GuiControlMultilineLabel))]
    public class MyGuiControlMultilineText : MyGuiControlBase
    {
        protected enum MyMultilineTextKeys
        {
            UP = 0,
            DOWN = 1,
            LEFT = 2,
            RIGHT = 3,
            C = 4,
            A = 5,
            V = 6,
            X=  7,
            HOME = 8,
            END = 9,
            DELETE = 10,
        }
        private class MyMultilineKeyTimeController
        {
            public Keys Key;

            /// <summary>
            /// This is not for converting key to string, but for controling repeated key input with delay
            /// </summary>
            public int LastKeyPressTime;

            public MyMultilineKeyTimeController(Keys key)
            {
                Key = key;
                LastKeyPressTime = MyGuiManager.FAREST_TIME_IN_PAST;
            }
        }

        #region Fields

        private float m_textScale;
        private float m_textScaleWithLanguage;
        private static readonly StringBuilder m_letterA = new StringBuilder("A");
        private static readonly StringBuilder m_lineHeightMeasure = new StringBuilder("Ajqypdbfgjl");

        protected readonly StringBuilder m_tmpOffsetMeasure = new StringBuilder();
        readonly MyVScrollbar m_scrollbar;
        private Vector2 m_scrollbarSize;
        protected MyRichLabel m_label;

        private bool m_drawScrollbar;
        private float m_scrollbarOffset;

        private bool m_selectable;

        //Carriage data for selectable texts
        private int m_carriageBlinkerTimer;
        protected int m_carriagePositionIndex;
        protected MyGuiControlMultilineSelection m_selection;
        private static MyMultilineKeyTimeController[] m_keys;

        protected int CarriagePositionIndex
        {
            get { return m_carriagePositionIndex; }
            set
            {
                var newPos = MathHelper.Clamp(value, 0, Text.Length);
                if (m_carriagePositionIndex != newPos)
                {
                    m_carriagePositionIndex = newPos;
                    if(!CarriageVisible())
                        ScrollToShowCarriage();
                }
            }
        }

        public bool Selectable
        {
            get { return m_selectable; }
        }

        virtual public StringBuilder Text
        {
            get { return m_text; }
            set
            {
                m_text.Clear();
                if (value != null)
                    m_text.AppendStringBuilder(value);
                RefreshText(false);
            }
        }
        protected StringBuilder m_text;

        public MyStringId TextEnum
        {
            get { return m_textEnum; }
            set
            {
                m_textEnum = value;
                RefreshText(true);
            }
        }
        private MyStringId m_textEnum;

        /// <summary>
        /// Says whether last set value was enum (true) or StringBuilder (false).
        /// </summary>
        private bool m_useEnum = true;
        #endregion

        public event LinkClicked OnLinkClicked;

        public MyFontEnum Font
        {
            get { return m_font; }
            set
            {
                if (m_font != value)
                {
                    m_font = value;
                    RefreshText(m_useEnum);
                }
            }
        }
        private MyFontEnum m_font;

        /// <summary>
        /// Gets or sets the color of the text.
        /// </summary>
        public Color TextColor { get; set; }

        public Vector2 TextSize
        {
            get { return m_label.GetSize(); }
        }

        public float ScrollbarOffset
        {
            get { return m_scrollbarOffset; }
            set 
            {
                m_scrollbarOffset = value;
                m_scrollbar.ChangeValue(m_scrollbarOffset);
                RecalculateScrollBar(); 
            }
        }

        public MyGuiControlMultilineText()
            : this(null)
        {
        }


        public MyGuiControlMultilineText(
            Vector2? position = null,
            Vector2? size = null,
            Vector4? backgroundColor = null,
            MyFontEnum font = MyFontEnum.Blue,
            float textScale = MyGuiConstants.DEFAULT_TEXT_SCALE,
            MyGuiDrawAlignEnum textAlign = MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP,
            StringBuilder contents = null,
            bool drawScrollbar = true,
            MyGuiDrawAlignEnum textBoxAlign = MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER,
            bool selectable = false)
            : base( position: position,
                    size: size,
                    colorMask: backgroundColor,
                    toolTip: null)
        {
            Font = font;
            TextScale = textScale;
            m_drawScrollbar = drawScrollbar;
            TextColor = new Color(Vector4.One);
            TextBoxAlign = textBoxAlign;
            m_selectable = selectable;

            m_scrollbar = new MyVScrollbar(this);
            m_scrollbarSize = new Vector2(0.0334f, MyGuiConstants.COMBOBOX_VSCROLLBAR_SIZE.Y);
            m_scrollbarSize = MyGuiConstants.COMBOBOX_VSCROLLBAR_SIZE;
            float minLineHeight = MyGuiManager.MeasureString(Font, m_lineHeightMeasure, TextScaleWithLanguage).Y;
            m_label = new MyRichLabel(ComputeRichLabelWidth(), minLineHeight);
            m_label.TextAlign = textAlign;
            m_text = new StringBuilder();
            m_selection = new MyGuiControlMultilineSelection();

            if (contents != null && contents.Length > 0)
                Text = contents;

            m_keys = new MyMultilineKeyTimeController[11];
            m_keys[(int)MyMultilineTextKeys.UP] = new MyMultilineKeyTimeController(Keys.Up);
            m_keys[(int)MyMultilineTextKeys.DOWN] = new MyMultilineKeyTimeController(Keys.Down);
            m_keys[(int)MyMultilineTextKeys.LEFT] = new MyMultilineKeyTimeController(Keys.Left);
            m_keys[(int)MyMultilineTextKeys.RIGHT] = new MyMultilineKeyTimeController(Keys.Right);
            
            m_keys[(int)MyMultilineTextKeys.C] = new MyMultilineKeyTimeController(Keys.C);
            m_keys[(int)MyMultilineTextKeys.A] = new MyMultilineKeyTimeController(Keys.A);
            m_keys[(int)MyMultilineTextKeys.V] = new MyMultilineKeyTimeController(Keys.V);
            m_keys[(int)MyMultilineTextKeys.X] = new MyMultilineKeyTimeController(Keys.X);
            m_keys[(int)MyMultilineTextKeys.HOME] = new MyMultilineKeyTimeController(Keys.Home);
            m_keys[(int)MyMultilineTextKeys.END] = new MyMultilineKeyTimeController(Keys.End);
            m_keys[(int)MyMultilineTextKeys.DELETE] = new MyMultilineKeyTimeController(Keys.Delete);
        }

        public override void Init(MyObjectBuilder_GuiControlBase objectBuilder)
        {
            base.Init(objectBuilder);

            m_label.MaxLineWidth = ComputeRichLabelWidth();
            var ob = (MyObjectBuilder_GuiControlMultilineLabel)objectBuilder;

            this.TextAlign    = (MyGuiDrawAlignEnum)ob.TextAlign;
            this.TextBoxAlign = (MyGuiDrawAlignEnum)ob.TextBoxAlign;
            this.TextScale    = ob.TextScale;
            this.TextColor    = new Color(ob.TextColor);
            this.Font         = ob.Font;

            MyStringId textEnum;
            if (Enum.TryParse<MyStringId>(ob.Text, out textEnum))
                TextEnum = textEnum;
            else
                Text = new StringBuilder(ob.Text);
        }

        public override MyObjectBuilder_GuiControlBase GetObjectBuilder()
        {
            var ob = (MyObjectBuilder_GuiControlMultilineLabel)base.GetObjectBuilder();

            ob.TextScale    = TextScale;
            ob.TextColor    = TextColor.ToVector4();
            ob.TextAlign    = (int)TextAlign;
            ob.TextBoxAlign = (int)TextBoxAlign;
            ob.Font         = Font;
            if (m_useEnum)
                ob.Text = TextEnum.ToString();
            else
                ob.Text = Text.ToString();

            return ob;
        }

        /// <summary>
        /// Sets the text to the given StringBuilder value.
        /// Layouts the controls.
        /// </summary>
        /// <param name="value"></param>
        public void RefreshText(bool useEnum)
        {
            if (m_label == null)
                return;
            m_label.Clear();
            m_useEnum = useEnum;
            if (useEnum)
                AppendText(MyTexts.Get(TextEnum));
            else
                AppendText(Text);

            if (Text.Length < CarriagePositionIndex)
                CarriagePositionIndex = Text.Length;
            m_selection.Reset(this);
        }

        public void AppendText(StringBuilder text)
        {
            AppendText(text, Font, TextScaleWithLanguage, TextColor.ToVector4());
        }

        public void AppendText(StringBuilder text, MyFontEnum font, float scale, Vector4 color)
        {
            m_label.Append(text, font, scale, color);
            RecalculateScrollBar();
        }

        public void AppendText(string text)
        {
            AppendText(text, Font, TextScaleWithLanguage, TextColor.ToVector4());
        }

        public void AppendText(string text, MyFontEnum font, float scale, Vector4 color)
        {
            m_label.Append(text, font, scale, color);
            RecalculateScrollBar();
        }

        public void AppendImage(string texture, Vector2 size, Vector4 color)
        {
            m_label.Append(texture, size, color);
            RecalculateScrollBar();
        }

        public void AppendLink(string url, string text)
        {
            m_label.AppendLink(url, text, TextScaleWithLanguage, OnLinkClickedInternal);
            RecalculateScrollBar();
        }

        private void OnLinkClickedInternal(string url)
        {
            if(OnLinkClicked != null)
                OnLinkClicked(this, url);
        }

        public void AppendLine()
        {
            m_label.AppendLine();
            RecalculateScrollBar();
        }

        public void Clear()
        {
            m_label.Clear();
            RecalculateScrollBar();
        }        

        private void RecalculateScrollBar()
        {
            float realHeight = m_label.GetSize().Y;

            bool vScrollbarVisible = Size.Y < realHeight;

            m_scrollbar.Visible = vScrollbarVisible;
            m_scrollbar.Init(realHeight, Size.Y);
            m_scrollbar.Layout(new Vector2(0.5f * Size.X  - m_scrollbar.Size.X, -0.5f * Size.Y), Size.Y);

            if (!m_drawScrollbar)
            {
                if (TextAlign == MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM ||
                    TextAlign == MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM ||
                    TextAlign == MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM)
                        //m_scrollbar.Value = realHeight;
                        m_scrollbar.Value = 0;
                else
                if (TextAlign == MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP ||
                   TextAlign == MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP ||
                   TextAlign == MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP)
                    //m_scrollbar.Value = 0;
                    m_scrollbar.Value = realHeight;
            }
        }

        private void DrawSelectionBackgrounds(MyRectangle2D textArea, float transitionAlpha)
        {
            var lines = Text.ToString().Substring(m_selection.Start, m_selection.Length).Split('\n');
            int currentPos = m_selection.Start;
            foreach (var line in lines)
            {
                Vector2 selectionPos = textArea.LeftTop + GetCarriageOffset(currentPos);
                Vector2 normalizedSize = GetCarriageOffset(currentPos + line.Length) - GetCarriageOffset(currentPos);
                Vector2 selectionSize = new Vector2(normalizedSize.X, GetCarriageHeight());
                MyGuiManager.DrawSpriteBatch(MyGuiConstants.BLANK_TEXTURE,
                        selectionPos,
                        selectionSize,
                        ApplyColorMaskModifiers(new Vector4(1, 1, 1, 0.5f), Enabled, transitionAlpha),
                        MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP
                   );

                currentPos += line.Length +1 ; //+1 because of \n that split cuts out
            }
        }

        public override void Draw(float transitionAlpha)
        {
            base.Draw(transitionAlpha);
            var textArea = new MyRectangle2D(Vector2.Zero, Size);
            textArea.LeftTop += GetPositionAbsoluteTopLeft();
            Vector2 carriageOffset = GetCarriageOffset(CarriagePositionIndex);

            var scissor = new RectangleF(textArea.LeftTop, textArea.Size);
            using (MyGuiManager.UsingScissorRectangle(ref scissor))
            {
                DrawSelectionBackgrounds(textArea, transitionAlpha);
                DrawText(m_scrollbar.Value);


                //  Draw carriage line
                //  Carriage blinker time is solved here in Draw because I want to be sure it will be drawn even in low FPS
                if (HasFocus && Selectable)
                {
                    //  This condition controls "blinking", so most often is carrier visible and blinks very fast
                    //  It also depends on FPS, but as we have max FPS set to 60, it won't go faster, nor will it omit a "blink".
                    int carriageInterval = m_carriageBlinkerTimer % 20;
                    if ((carriageInterval >= 0) && (carriageInterval <= 15))
                    {
                        MyGuiManager.DrawSpriteBatch(MyGuiConstants.BLANK_TEXTURE,
                            textArea.LeftTop + carriageOffset,
                            1,
                            GetCarriageHeight(),
                            ApplyColorMaskModifiers(Vector4.One, Enabled, transitionAlpha),
                            MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP);
                    }
                }
                m_carriageBlinkerTimer++;

                if (m_drawScrollbar)
                    m_scrollbar.Draw(ApplyColorMaskModifiers(ColorMask, Enabled, transitionAlpha));
            }
            //m_scrollbar.DebugDraw();
        }

        public override MyGuiControlBase HandleInput()
        {
            MyGuiControlBase baseResult = base.HandleInput();

            if (HasFocus && Selectable)
            {
                //  Move left
                if ((MyInput.Static.IsKeyPress(MyKeys.Left)))
                {
                    if ((IsEnoughDelay(MyMultilineTextKeys.LEFT, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY)))
                    {
                        if (MyInput.Static.IsAnyCtrlKeyPressed())
                            CarriagePositionIndex = GetPreviousSpace();
                        else
                            CarriagePositionIndex--;

                        UpdateLastKeyPressTimes(MyMultilineTextKeys.LEFT);
                        if (MyInput.Static.IsAnyShiftKeyPressed())
                            m_selection.SetEnd(this);
                        else
                            m_selection.Reset(this);
                    }
                    return this;
                }

                //  Move right
                if ((MyInput.Static.IsKeyPress(MyKeys.Right)))
                {
                    if ((IsEnoughDelay(MyMultilineTextKeys.RIGHT, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY)))
                    {
                        if (MyInput.Static.IsAnyCtrlKeyPressed())
                            CarriagePositionIndex = GetNextSpace();
                        else
                            ++CarriagePositionIndex;
                        UpdateLastKeyPressTimes(MyMultilineTextKeys.RIGHT);
                        if (MyInput.Static.IsAnyShiftKeyPressed())
                            m_selection.SetEnd(this);
                        else
                            m_selection.Reset(this);
                    }
                    return this;
                }

                //  Move Down
                if ((MyInput.Static.IsKeyPress(MyKeys.Down)))
                {
                    if ((IsEnoughDelay(MyMultilineTextKeys.DOWN, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY)))
                    {
                        CarriagePositionIndex = GetIndexUnderCarriage(CarriagePositionIndex);
                        UpdateLastKeyPressTimes(MyMultilineTextKeys.DOWN);
                        if (MyInput.Static.IsAnyShiftKeyPressed())
                            m_selection.SetEnd(this);
                        else
                            m_selection.Reset(this);
                    }
                    return this;
                }

                //  Move Up
                if ((MyInput.Static.IsKeyPress(MyKeys.Up)))
                {
                    if ((IsEnoughDelay(MyMultilineTextKeys.UP, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY)))
                    {
                        CarriagePositionIndex = GetIndexOverCarriage(CarriagePositionIndex);
                        UpdateLastKeyPressTimes(MyMultilineTextKeys.UP);
                        if (MyInput.Static.IsAnyShiftKeyPressed())
                            m_selection.SetEnd(this);
                        else
                            m_selection.Reset(this);
                    }
                    return this;
                }
              
                //Copy
                if (MyInput.Static.IsNewKeyPressed(MyKeys.C) && IsEnoughDelay(MyMultilineTextKeys.C, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY) && MyInput.Static.IsAnyCtrlKeyPressed())
                {
                    UpdateLastKeyPressTimes(MyMultilineTextKeys.C);
                    m_selection.CopyText(this);
                }

                //Select All
                if (MyInput.Static.IsNewKeyPressed(MyKeys.A) && IsEnoughDelay(MyMultilineTextKeys.A, MyGuiConstants.TEXTBOX_MOVEMENT_DELAY) && MyInput.Static.IsAnyCtrlKeyPressed())
                {
                    m_selection.SelectAll(this);
                    return this;
                }

            }

            //scroll
            bool captured = false;
            var deltaWheel = MyInput.Static.DeltaMouseScrollWheelValue();
            if (IsMouseOver && deltaWheel != 0)
            {
                m_scrollbar.ChangeValue(-0.0005f * deltaWheel);
                captured = true;
            }


            if (m_drawScrollbar)
            {
                bool capturedScrollbar = m_scrollbar.HandleInput();

                if (capturedScrollbar || captured)
                    return this;
            }
            if (IsMouseOver && m_label.HandleInput(GetPositionAbsoluteTopLeft(), m_scrollbar.Value))
                return this;

            if (Selectable)
            {
                if (MyInput.Static.IsNewLeftMousePressed())
                {
                    if (IsMouseOver)
                    {
                        m_selection.Dragging = true;
                        CarriagePositionIndex = GetCarriagePositionFromMouseCursor();
                        if (MyInput.Static.IsAnyShiftKeyPressed())
                            m_selection.SetEnd(this);
                        else
                            m_selection.Reset(this);
                        return this;
                    }
                    else
                        m_selection.Reset(this);
                }

                else if (MyInput.Static.IsNewLeftMouseReleased())
                {
                    m_selection.Dragging = false;
                }

                //user holding the mouse button
                else if (m_selection.Dragging)
                {
                    //If inside, we update selection and move the carriage (dragging what you want to select)
                    if (IsMouseOver)
                    {
                        CarriagePositionIndex = GetCarriagePositionFromMouseCursor();
                        m_selection.SetEnd(this);
                    }

                    //Otherwise, do the "scroll along with selection" effect
                    else if (HasFocus)
                    {
                        Vector2 mousePos = MyGuiManager.MouseCursorPosition;
                        Vector2 positionTopLeft = GetPositionAbsoluteTopLeft();
                        if (mousePos.Y < positionTopLeft.Y)
                            m_scrollbar.ChangeValue(Position.Y - mousePos.Y);
                        else if (mousePos.Y > positionTopLeft.Y + Size.Y)
                            m_scrollbar.ChangeValue(mousePos.Y - positionTopLeft.Y - Size.Y);
                    }
                }
            }

            return baseResult;
        }
       
        /// <summary>
        /// Draws the text with the offset given by the scrollbar.
        /// </summary>
        /// <param name="offset">Indicates how low is the scrollbar (and how many beginning lines are skipped)</param>
        private void DrawText(float offset)
        {
            Vector2 position = GetPositionAbsoluteTopLeft();
            Vector2 drawSizeMax = Size;
            if (m_drawScrollbar && m_scrollbar.Visible)
                drawSizeMax.X -= m_scrollbar.Size.X;

            var textSize = TextSize;
            if (textSize.X < drawSizeMax.X)
            {
                switch (TextBoxAlign)
                {
                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM:
                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER:
                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP:
                        break;

                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM:
                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER:
                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP:
                        position.X += (drawSizeMax.X - textSize.X) * 0.5f;
                        break;

                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM:
                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER:
                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP:
                        position.X += (drawSizeMax.X - textSize.X);
                        break;
                }
                drawSizeMax.X = textSize.X;
            }

            if (textSize.Y < drawSizeMax.Y)
            {
                switch (TextBoxAlign)
                {
                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_TOP:
                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_TOP:
                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_TOP:
                        break;

                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_CENTER:
                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_CENTER:
                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_CENTER:
                        position.Y += (drawSizeMax.Y - textSize.Y) * 0.5f;
                        break;

                    case MyGuiDrawAlignEnum.HORISONTAL_LEFT_AND_VERTICAL_BOTTOM:
                    case MyGuiDrawAlignEnum.HORISONTAL_CENTER_AND_VERTICAL_BOTTOM:
                    case MyGuiDrawAlignEnum.HORISONTAL_RIGHT_AND_VERTICAL_BOTTOM:
                        position.Y += (drawSizeMax.Y - textSize.Y);
                        break;
                }
                drawSizeMax.Y = textSize.Y;
            }

            m_label.Draw(position, offset, drawSizeMax);
        }

        public float TextScale
        {
            get { return m_textScale; }
            set
            {
                m_textScale = value;
                TextScaleWithLanguage = value * MyGuiManager.LanguageTextScale;
            }
        }

        public float TextScaleWithLanguage
        {
            get { return m_textScaleWithLanguage; }
            private set { m_textScaleWithLanguage = value; }
        }

        /// <summary>
        /// Alignment of text as if you were specifying it in MS Word. This controls the appearance of text itself.
        /// </summary>
        public MyGuiDrawAlignEnum TextAlign
        {
            get { return m_label.TextAlign; }
            set { m_label.TextAlign = value; }
        }

        /// <summary>
        /// Alignment of box containing text within the control. Eg. if text does not fill whole control horizontally,
        /// this will specify how should sides of the box be aligned.
        /// </summary>
        public MyGuiDrawAlignEnum TextBoxAlign
        {
            get;
            set;
        }

        protected override void OnSizeChanged()
        {
            if (m_label != null)
            {
                m_label.MaxLineWidth = ComputeRichLabelWidth();
                RefreshText(m_useEnum);
            }
            if (m_drawScrollbar)
                RecalculateScrollBar();

            base.OnSizeChanged();
        }

        protected virtual float ComputeRichLabelWidth()
        {
            float res = Size.X;// -2f * MyGuiConstants.MULTILINE_LABEL_BORDER.X;
            if (m_drawScrollbar)
                res -= m_scrollbarSize.X;
            return res;
        }

        #region carriage

        private bool CarriageVisible()
        {
            Vector2 offset = GetCarriageOffset(CarriagePositionIndex);
            float height = GetCarriageHeight();
            return (offset.Y >= 0 && offset.Y +  height <= Size.Y);
        }

        virtual protected int GetCarriagePositionFromMouseCursor()
        {
            Vector2 mouseRelative = MyGuiManager.MouseCursorPosition - GetPositionAbsoluteTopLeft();
            int closestIndex = 0;
            float closestDistance = float.MaxValue;
            for (int i = 0; i <= m_text.Length; i++)
            {
                Vector2 charPosition = GetCarriageOffset(i);
                float charDistance = Vector2.Distance(charPosition, mouseRelative);
                if (charDistance < closestDistance)
                {
                    closestDistance = charDistance;
                    closestIndex = i;
                }
            }

            return closestIndex;
        }


        protected virtual Vector2 GetCarriageOffset(int idx)
        {
            Vector2 output = new Vector2(0, -m_scrollbar.Value);
            int start = GetLineStartIndex(idx);
            if (idx - start > 0)
            {
                m_tmpOffsetMeasure.Clear();
                m_tmpOffsetMeasure.AppendSubstring(Text, start, idx - start);
                output.X = MyGuiManager.MeasureString(Font, m_tmpOffsetMeasure, TextScaleWithLanguage).X;
            }
            if (start - 1 > 0)
            {
                m_tmpOffsetMeasure.Clear();
                m_tmpOffsetMeasure.AppendSubstring(Text, 0, start - 1);
                output.Y = MyGuiManager.MeasureString(Font, m_tmpOffsetMeasure, TextScaleWithLanguage).Y - m_scrollbar.Value;
            }
            return output;
        }

        private float GetCarriageHeight()
        {
            return MyGuiManager.MeasureString(Font, m_letterA, TextScaleWithLanguage).Y;
        }

        //if it's out of scroll range, we scroll down
        private void ScrollToShowCarriage()
        {
            Vector2 carriagePos = GetCarriageOffset(CarriagePositionIndex);
            float carriageHeight = GetCarriageHeight();
            if (carriagePos.Y + carriageHeight > Size.Y)
                m_scrollbar.ChangeValue(carriagePos.Y + carriageHeight - Size.Y);

            //if it's out of scroll range, we scroll up
            if (carriagePos.Y < 0)
                m_scrollbar.ChangeValue(carriagePos.Y);
        }

        protected virtual int GetLineStartIndex(int idx)
        {
            var output = Text.ToString().Substring(0, idx).LastIndexOf('\n');
            return (output == -1) ? 0 : output;
        }

        protected int GetLineEndIndex(int idx)
        {
            if (idx == Text.Length)
                return Text.Length;
            var output = Text.ToString().Substring(idx).IndexOf('\n');
            return (output == -1) ? Text.Length : (idx + output);
        }

        virtual protected int GetIndexUnderCarriage(int idx)
        {
            int start = GetLineStartIndex(idx);
            int end = GetLineEndIndex(idx);
            return end + idx - start + ((start == 0) ? 1 : 0);
        }

        virtual protected int GetIndexOverCarriage(int idx)
        {
            int start = GetLineStartIndex(idx);
            int start2 = start;
            if (start > 0)
                start2 = GetLineStartIndex(start - 1);
            int end = GetLineEndIndex(idx);
            return start2 + idx - start - ((start2 == 0) ? 1 : 0);
        }

        /// <summary>
        /// gets the position of the first space to the left of the carriage or 0 if there isn't any
        /// </summary>
        /// <returns></returns>
        private int GetPreviousSpace()
        {
            if (CarriagePositionIndex == 0)
                return 0;

            int lastSpace = m_text.ToString().Substring(0, CarriagePositionIndex).LastIndexOf(" ");
            int lastLine = m_text.ToString().Substring(0, CarriagePositionIndex).LastIndexOf("\n");

            if (lastSpace == -1 && lastLine == -1)
                return 0;

            return Math.Max(lastSpace, lastLine);
        }

        /// <summary>
        /// gets the position of the first space to the right of the carriage or the text length if there isn't any
        /// </summary>
        /// <returns></returns>
        private int GetNextSpace()
        {
            if (CarriagePositionIndex == m_text.Length)
                return m_text.Length;

            int nextSpace = m_text.ToString().Substring(CarriagePositionIndex + 1).IndexOf(" ");
            int nextLine = m_text.ToString().Substring(CarriagePositionIndex + 1).IndexOf("\n");

            if (nextSpace == -1 && nextLine == -1)
                return m_text.Length;

            return CarriagePositionIndex + Math.Min(nextSpace, nextLine) + 1;
        }

        protected bool IsEnoughDelay(MyMultilineTextKeys key, int forcedDelay)
        {
            MyMultilineKeyTimeController keyEx = m_keys[(int)key];
            if (keyEx == null) return true;

            return ((MyGuiManager.TotalTimeInMilliseconds - keyEx.LastKeyPressTime) > forcedDelay);
        }

        protected void UpdateLastKeyPressTimes(MyMultilineTextKeys key)
        {
            //  This will reset the counter so it starts blinking whenever we enter the textbox
            //  And also when user presses a lot of keys, it won't blink for a while
            m_carriageBlinkerTimer = 0;

            //  Making delays between one long key press
            MyMultilineKeyTimeController keyEx = m_keys[(int)key];
            if (keyEx != null)
            {
                keyEx.LastKeyPressTime = MyGuiManager.TotalTimeInMilliseconds;
            }
        }
        #endregion
        #region selection
        protected class MyGuiControlMultilineSelection
        {
            protected int m_startIndex, m_endIndex;
            private string ClipboardText;
            private bool m_dragging = false;
            public bool Dragging
            {
                get { return m_dragging; }
                set { m_dragging = value; }
            }

            public MyGuiControlMultilineSelection()
            {
                m_startIndex = 0;
                m_endIndex = 0;
            }

            public int Start
            {
                get { return Math.Min(m_startIndex, m_endIndex); }
            }

            public int End
            {
                get { return Math.Max(m_startIndex, m_endIndex); }
            }

            public int Length
            {
                get { return End - Start; }
            }

            public void SetEnd(MyGuiControlMultilineText sender)
            {
                m_endIndex = MathHelper.Clamp(sender.CarriagePositionIndex, 0, sender.Text.Length);
            }


            public void Reset(MyGuiControlMultilineText sender)
            {
                m_startIndex = m_endIndex = MathHelper.Clamp(sender.CarriagePositionIndex, 0, sender.Text.Length);
            }

            public void SelectAll(MyGuiControlMultilineText sender)
            {
                m_startIndex = 0;
                m_endIndex = sender.Text.Length;
                sender.CarriagePositionIndex = sender.Text.Length;
            }

            public void EraseText(MyGuiControlMultilineText sender)
            {
                if (Start == End)
                    return;
                StringBuilder prefix = new StringBuilder(sender.Text.ToString().Substring(0, Start));
                StringBuilder suffix = new StringBuilder(sender.Text.ToString().Substring(End));
                sender.CarriagePositionIndex = Start;
                sender.Text = prefix.Append(suffix);
            }

            public void CopyText(MyGuiControlMultilineText sender)
            {
                ClipboardText = Regex.Replace(sender.Text.ToString().Substring(Start, Length), "\n", "\r\n");
                Thread myth;
                myth = new Thread(new System.Threading.ThreadStart(CopyToClipboard));
                myth.ApartmentState = ApartmentState.STA;
                myth.Start();
            }

            public void CutText(MyGuiControlMultilineText sender)
            {
                //First off, we have to copy
                CopyText(sender);

                //Then we cut the text away from the form
                EraseText(sender);
            }

            public void PasteText(MyGuiControlMultilineText sender)
            {
                //First we erase the selection
                EraseText(sender);
                var prefix = sender.Text.ToString().Substring(0, sender.CarriagePositionIndex);
                var suffix = sender.Text.ToString().Substring(sender.CarriagePositionIndex);
                Thread myth;

                myth = new Thread(new System.Threading.ThreadStart(PasteFromClipboard));
                myth.ApartmentState = ApartmentState.STA;
                myth.Start();

                //We have to wait for the thread to end to make sure we got the text
                myth.Join();

                sender.Text = new StringBuilder(prefix).Append(Regex.Replace(ClipboardText, "\r\n", " \n")).Append(suffix);
                sender.CarriagePositionIndex = prefix.Length + ClipboardText.Length;
                Reset(sender);
            }

            void PasteFromClipboard()
            {
                ClipboardText = Clipboard.GetText();
            }

            void CopyToClipboard()
            {
                if (ClipboardText != "")
                    Clipboard.SetText(ClipboardText);
            }

        }
        #endregion

        protected float ScrollbarValue 
        {
            get 
            {
                return m_scrollbar.Value;
            }
        }
    }
}
