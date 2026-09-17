using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Icons.Ui
{
    /// <summary>
    /// What an apply is about to do to the org, said as a list rather than as a paragraph.
    ///
    /// Every field is what will be written, not a sentence about it: the dialog is the one place
    /// that turns them into words, so there is one copy of that wording and the caller cannot
    /// half-build it. The four trailing lines are the ones that are only sometimes there - a
    /// licence that asks for a credit, an icon being replaced, the classic PNG slots changing
    /// hands, a name under another publisher's prefix - and each is left null when it has
    /// nothing to say.
    /// </summary>
    internal sealed class ApplyConfirmation
    {
        public string IconName;
        public string TableName;
        public string SolutionName;
        public string WebResourceName;
        public string AttributionName;

        public string Credit;
        public string Replacing;
        public string Handover;
        public string Concern;
    }

    /// <summary>
    /// The gate in front of the only thing this tool does to somebody's environment.
    ///
    /// A window of its own rather than a message box, for one reason: this is a list of four
    /// writes and a message box can only be told a paragraph. Four bullets are read at a glance
    /// and counted; the same four folded into a sentence - "writes the web resource X and the
    /// attribution page beside it, Y, adding whichever is new to Z, and publishes the table" -
    /// has to be taken apart before it can be checked, which at the last gate before a round
    /// trip is exactly the wrong amount of work. It also lets the two names the question is
    /// about, and the solution, stand in italics, so the sentence reads as a sentence instead
    /// of as three proper nouns run together.
    ///
    /// What it keeps from the message box it replaces is everything the rest of the world knows
    /// it by: the caption, the question icon, OK and Cancel in that order, Escape for Cancel -
    /// and an OK button that still answers to <c>GetDlgItem(dialog, IDOK)</c>, which is how the
    /// test harnesses press it. WinForms gives its controls no dialog id at all, so that one is
    /// put on by hand once the handle exists.
    /// </summary>
    internal sealed class ConfirmApply : Form
    {
        /// <summary>The text column, in logical pixels. Everything else is laid out around it.</summary>
        private const int ContentWidth = 372;

        /// <summary>The air between the body and the window, on every side.</summary>
        private const int Air = 12;

        private const int IDOK = 1;
        private const int GWL_ID = -12;

        [DllImport("user32.dll", SetLastError = true)]
        private static extern int SetWindowLong(IntPtr window, int index, int value);

        private readonly RichTextBox _body;
        private readonly Button _ok;

        /// <summary>Opens the gate over its owner and waits. OK means go.</summary>
        public static DialogResult Ask(IWin32Window owner, ApplyConfirmation what)
        {
            if (what == null) throw new ArgumentNullException(nameof(what));

            using (var dialog = new ConfirmApply(what))
            {
                return dialog.ShowDialog(owner);
            }
        }

        private ConfirmApply(ApplyConfirmation what)
        {
            // A bare Form is Microsoft Sans Serif 8.25pt, which is not what any dialog in Windows
            // has looked like for twenty years. This is the font a message box uses.
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;

            // The caption a message box carried, unchanged: it is what the harnesses recognise
            // this window by, and what somebody reading a screenshot recognises it by too.
            Text = "Set table icon";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SystemColors.Control;

            var question = new PictureBox
            {
                Image = SystemIcons.Question.ToBitmap(),
                SizeMode = PictureBoxSizeMode.AutoSize,
                Location = new Point(Air, Air)
            };

            _body = new RichTextBox
            {
                // A read-only rich text box is the one control in this toolkit that will wrap a
                // sentence with a word of it in italics. Everything that makes it look like a
                // box rather than like text is turned off.
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Control,
                ScrollBars = RichTextBoxScrollBars.None,
                WordWrap = true,
                TabStop = false,
                DetectUrls = false,
                Cursor = Cursors.Default,
                Location = new Point(question.Right + Air, Air),
                Width = ContentWidth,
                Height = 10
            };

            // Height comes from the text, which is only known once the control has laid it out.
            _body.ContentsResized += (s, e) => _body.Height = e.NewRectangle.Height + 2;

            // Nothing in the body is meant to be selected, and a selection somebody cannot see
            // the point of is a selection they think they have broken something with.
            _body.GotFocus += (s, e) => _ok.Focus();

            _ok = new Button { Text = "OK", DialogResult = DialogResult.OK, AutoSize = true };
            var cancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };

            // Wide enough to be a dialog button rather than as wide as the word on it.
            _ok.Size = cancel.Size = new Size(Font.Height * 6, Font.Height + 12);

            AcceptButton = _ok;
            CancelButton = cancel;

            Controls.Add(question);
            Controls.Add(_body);
            Controls.Add(_ok);
            Controls.Add(cancel);

            // After the control is on the form, and not before. Joining a form hands a control
            // the form's font, and a rich text box answers a change of font by putting that font
            // on everything it is holding - which takes the italics off the two names the
            // question is about. Written first, they were gone by the time it was on screen.
            _body.Rtf = Rtf(what);

            Fit();
        }

        /// <summary>
        /// The window around the text: the buttons under it, right aligned, and the form around
        /// both. Done here rather than by a layout panel because the body's height arrives from
        /// the control after its text does, and everything below it moves when it does.
        /// </summary>
        private void Fit()
        {
            var right = _body.Right;
            var top = _body.Bottom + Air + 4;

            var cancel = (Button)CancelButton;
            cancel.Location = new Point(right - cancel.Width, top);
            _ok.Location = new Point(cancel.Left - _ok.Width - 8, top);

            ClientSize = new Size(right + Air, _ok.Bottom + Air);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);

            // What makes GetDlgItem(this, IDOK) find the OK button. WinForms creates its controls
            // with no dialog id, so anything driving this window the way Windows drives a dialog
            // - the harnesses here, and every automation tool - would find nothing and fall back
            // to closing the window, which is Cancel.
            //
            // Handle rather than IsHandleCreated: a child's window is not made when its form's
            // is, it is made when something asks for it, and this is the asking.
            SetWindowLong(_ok.Handle, GWL_ID, IDOK);
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            _ok.Focus();
        }

        /// <summary>
        /// The whole of what the window says, as RTF. One place, so the question, the list and
        /// the lines that only sometimes appear are all in the same voice.
        /// </summary>
        private string Rtf(ApplyConfirmation what)
        {
            var text = new StringBuilder();

            text.Append(@"{\rtf1\ansi\ansicpg1252\deff0{\fonttbl{\f0\fnil\fcharset0 ")
                .Append(Font.Name)
                .Append(@";}}\viewkind4\uc1\f0\fs")
                .Append(((int)Math.Round(Font.SizeInPoints * 2)).ToString(CultureInfo.InvariantCulture));

            Paragraph(text,
                "Set " + Italic(what.IconName) + " as the icon for " + Italic(what.TableName) + "?");
            Paragraph(text, "This will:");

            Bullet(text, "write web resource: " + Escape(what.WebResourceName));
            Bullet(text, "write attribution page: " + Escape(what.AttributionName));
            Bullet(text, "add both to " + Italic(what.SolutionName) + " solution");
            Bullet(text, "publish table");

            // The four that are only sometimes there. Paragraphs rather than bullets: none of
            // them is a thing being done, they are all things worth knowing about the four above.
            foreach (var note in new[] { what.Credit, what.Replacing, what.Handover, what.Concern })
            {
                if (!string.IsNullOrEmpty(note)) Note(text, Escape(note));
            }

            text.Append('}');
            return text.ToString();
        }

        /// <summary>A paragraph of already-escaped rich text, with a line of air under it.</summary>
        private static void Paragraph(StringBuilder text, string body)
        {
            text.Append(@"\pard\sa120\fi0\li0 ").Append(body).Append(@"\par");
        }

        /// <summary>
        /// One of the lines that only sometimes appear, under the list. Air above as well as
        /// below, because what is over it is a bullet with a bullet's small gap under it, and
        /// without it the note reads as a fifth thing the apply is about to do.
        /// </summary>
        private static void Note(StringBuilder text, string body)
        {
            text.Append(@"\pard\sb160\sa60\fi0\li0 ").Append(body).Append(@"\par");
        }

        /// <summary>
        /// One item of the list. Hanging indent, so a bullet that wraps keeps its second line
        /// under its first word rather than under the bullet.
        /// </summary>
        private static void Bullet(StringBuilder text, string body)
        {
            // The tab stop is set on the line rather than left to the default one, which is half
            // an inch in and puts the bullet and its words at opposite ends of the indent.
            //
            // The bullet itself goes through Escape rather than being written here as an RTF
            // escape of its own: a backslash-u in this file is a C# escape before it is ever an
            // RTF one, and the compiler gets there first.
            text.Append(@"\pard\sa40\fi-160\li340\tx340 ").Append(Escape("•"))
                .Append(@"\tab ").Append(body).Append(@"\par");
        }

        private static string Italic(string value)
        {
            return @"\i " + Escape(value) + @"\i0 ";
        }

        /// <summary>
        /// Plain text as rich text. Every name here comes from an org or from a library, so none
        /// of it can be trusted to be ASCII or to be free of the three characters RTF reads.
        /// </summary>
        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";

            var text = new StringBuilder(value.Length + 8);
            foreach (var letter in value)
            {
                switch (letter)
                {
                    case '\\': text.Append(@"\\"); break;
                    case '{': text.Append(@"\{"); break;
                    case '}': text.Append(@"\}"); break;
                    case '\r': break;
                    case '\n': text.Append(@"\line "); break;
                    default:
                        if (letter < 128) text.Append(letter);
                        else text.Append(@"\u")
                                 .Append(((int)letter).ToString(CultureInfo.InvariantCulture))
                                 .Append('?');
                        break;
                }
            }

            return text.ToString();
        }
    }
}
