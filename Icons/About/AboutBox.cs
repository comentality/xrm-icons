using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Comentality.Xtb.About
{
    /// <summary>
    /// What the tool is, who to tell when it is wrong, and - for a tool that redistributes
    /// somebody else's work - who drew it.
    ///
    /// The window is the size of what it is showing. A tool with no licence notices has no line
    /// offering them and no room set aside for them; Icons, which carries four icon families,
    /// grows by the height of the notices when somebody asks to see them and gives it back when
    /// they are done. Nothing opens a second window, and nothing sits in a half-empty one.
    ///
    /// Shared verbatim between the Comentality tools - see <see cref="ToolAbout"/> for what that
    /// means for the namespace.
    /// </summary>
    public sealed class AboutBox : Form
    {
        /// <summary>The content column, in logical pixels. Everything else is laid out around it.</summary>
        private const int ContentWidth = 452;

        private const string Folded = "▶";
        private const string Unfolded = "▼";

        private readonly ToolAbout _about;
        private LinkLabel _disclosure;
        private TextBox _notices;

        private AboutBox(ToolAbout about)
        {
            _about = about;

            // A bare Form is Microsoft Sans Serif 8.25pt, which is not what any dialog in Windows
            // has looked like for twenty years. This is the font a message box uses.
            Font = SystemFonts.MessageBoxFont;
            AutoScaleMode = AutoScaleMode.Font;

            Text = "About " + about.Name;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ShowInTaskbar = false;
            ShowIcon = false;
            StartPosition = FormStartPosition.CenterParent;
            BackColor = SystemColors.Control;
            Padding = new Padding(12);
            AutoSize = true;
            AutoSizeMode = AutoSizeMode.GrowAndShrink;

            Controls.Add(BuildBody());
        }

        /// <summary>Opens the box over its owner and waits.</summary>
        public static void Show(IWin32Window owner, ToolAbout about)
        {
            if (about == null) throw new ArgumentNullException(nameof(about));

            using (var box = new AboutBox(about))
            {
                box.ShowDialog(owner);
            }
        }

        private Control BuildBody()
        {
            var rows = new TableLayoutPanel
            {
                ColumnCount = 1,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top
            };

            rows.Controls.Add(BuildIdentity());
            rows.Controls.Add(Rule());
            rows.Controls.Add(BuildFeedback());

            if (!string.IsNullOrWhiteSpace(_about.Notices))
            {
                rows.Controls.Add(Rule());
                rows.Controls.Add(BuildDisclosure());
                rows.Controls.Add(BuildNotices());
            }

            rows.Controls.Add(BuildFooter());

            return rows;
        }

        /// <summary>The logo, the name, the build, the description and the three links.</summary>
        private Control BuildIdentity()
        {
            var identity = new TableLayoutPanel
            {
                ColumnCount = 2,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            var logoWidth = _about.Logo == null ? 0 : 56;

            identity.Controls.Add(new PictureBox
            {
                Image = _about.Logo,
                Size = new Size(48, 48),
                SizeMode = PictureBoxSizeMode.Zoom,
                Margin = new Padding(0, 2, 8, 0),
                Visible = _about.Logo != null
            }, 0, 0);

            var text = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                Width = ContentWidth - logoWidth
            };

            text.Controls.Add(new Label
            {
                Text = _about.Name,
                Font = new Font(Font.FontFamily, Font.Size + 4.5f, FontStyle.Regular),
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 1)
            });

            text.Controls.Add(new Label
            {
                Text = _about.Version,
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Margin = new Padding(0, 0, 0, 6)
            });

            if (!string.IsNullOrWhiteSpace(_about.Description))
            {
                text.Controls.Add(new Label
                {
                    Text = _about.Description,
                    AutoSize = true,
                    MaximumSize = new Size(ContentWidth - logoWidth, 0),
                    Margin = new Padding(0, 0, 0, 8)
                });
            }

            text.Controls.Add(BuildLinks());

            identity.Controls.Add(text, 1, 0);
            return identity;
        }

        private Control BuildLinks()
        {
            var links = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0),
                WrapContents = true,
                MaximumSize = new Size(ContentWidth - (_about.Logo == null ? 0 : 56), 0)
            };

            // The site reads as the address it is, because that is what somebody types into a
            // browser on the machine next to this one. The other two are named for what they do.
            links.Controls.Add(Link(_about.PageUrl.Replace("https://", ""), _about.PageUrl));

            if (_about.RepositoryUrl != null)
            {
                links.Controls.Add(Dot());
                links.Controls.Add(Link("What's new", _about.ReleaseNotesUrl));
                links.Controls.Add(Dot());
                links.Controls.Add(Link("Source", _about.RepositoryUrl));
            }

            return links;
        }

        private Control BuildFeedback()
        {
            var group = new GroupBox
            {
                Text = "Something wrong, or missing?",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0, 2, 0, 0),
                Padding = new Padding(10, 4, 10, 10)
            };

            var inside = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.TopDown,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Dock = DockStyle.Top,
                Margin = new Padding(0)
            };

            inside.Controls.Add(new Label
            {
                Text = "Tell us what broke, what is missing, or what you would rather it did. " +
                       "Copy diagnostics puts your versions on the clipboard - read it before you " +
                       "paste it. Nothing leaves this window on its own.",
                AutoSize = true,
                MaximumSize = new Size(ContentWidth - 26, 0),
                Margin = new Padding(0, 0, 0, 9)
            });

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.LeftToRight,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Margin = new Padding(0)
            };

            var write = new Button
            {
                Text = "Write to " + ToolAbout.FeedbackAddress,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(0, 0, 8, 0)
            };
            write.Click += (s, e) => Write();

            var copy = new Button
            {
                Text = "Copy diagnostics",
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 2, 8, 2),
                Margin = new Padding(0)
            };
            copy.Click += (s, e) => CopyDiagnostics();

            buttons.Controls.Add(write);
            buttons.Controls.Add(copy);
            inside.Controls.Add(buttons);

            group.Controls.Add(inside);
            AcceptButton = write;

            return group;
        }

        private Control BuildDisclosure()
        {
            _disclosure = Link(Folded + "  " + _about.NoticesCaption, null);
            _disclosure.Margin = new Padding(0, 2, 0, 0);
            _disclosure.LinkClicked += (s, e) => Toggle();
            return _disclosure;
        }

        private Control BuildNotices()
        {
            _notices = new TextBox
            {
                Text = _about.Notices,
                Multiline = true,
                ReadOnly = true,
                WordWrap = true,
                ScrollBars = ScrollBars.Vertical,
                BackColor = SystemColors.Window,
                Font = new Font(FontFamily.GenericMonospace, Font.Size - 0.5f),
                Size = new Size(ContentWidth, 170),
                Margin = new Padding(0, 6, 0, 0),
                Visible = false
            };

            return _notices;
        }

        private Control BuildFooter()
        {
            var footer = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Width = ContentWidth,
                Margin = new Padding(0, 10, 0, 0)
            };

            var ok = new Button
            {
                Text = "OK",
                DialogResult = DialogResult.OK,
                Size = new Size(80, 25),
                Margin = new Padding(0)
            };

            footer.Controls.Add(ok);
            CancelButton = ok;

            return footer;
        }

        /// <summary>
        /// Shows the notices, or puts them away. The window is sized by what is in it, so this is
        /// all it takes - except at the bottom of a screen, where growing downwards would put the
        /// OK button somewhere nobody can click.
        /// </summary>
        private void Toggle()
        {
            var showing = !_notices.Visible;
            _notices.Visible = showing;
            _disclosure.Text = (showing ? Unfolded : Folded) + "  " + _about.NoticesCaption;

            if (!showing) return;

            var screen = Screen.FromControl(this).WorkingArea;
            if (Bottom > screen.Bottom)
            {
                Top = Math.Max(screen.Top, screen.Bottom - Height);
            }
        }

        private void Write()
        {
            try
            {
                Process.Start(_about.MailTo().ToString());
            }
            catch (Win32Exception)
            {
                NoMailClient();
            }
            catch (SystemException)
            {
                // Shell execute reports a missing handler in more than one shape, and none of
                // them is worth taking the tool down for.
                NoMailClient();
            }
        }

        private void NoMailClient()
        {
            MessageBox.Show(this,
                "This machine has no mail client set up, so nothing opened." + Environment.NewLine +
                Environment.NewLine +
                "Write to " + ToolAbout.FeedbackAddress + " from wherever you do read mail. " +
                "Copy diagnostics will give you the versions to paste in.",
                "Write to " + ToolAbout.FeedbackAddress,
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void CopyDiagnostics()
        {
            try
            {
                Clipboard.SetText(_about.Diagnostics());
            }
            catch (ExternalException)
            {
                // Another process had the clipboard open. It is a queue of one and everything on
                // the machine is in it.
                MessageBox.Show(this,
                    "Windows would not hand over the clipboard - something else on this machine " +
                    "is holding it. Try again in a moment.",
                    "Copy diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MessageBox.Show(this,
                "Copied. Paste it into your mail:" + Environment.NewLine +
                Environment.NewLine +
                _about.Diagnostics(),
                "Copy diagnostics", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private LinkLabel Link(string text, string url)
        {
            var link = new LinkLabel
            {
                Text = text,
                AutoSize = true,
                Margin = new Padding(0)
            };

            if (url != null)
            {
                link.LinkClicked += (s, e) => Open_(url);
            }

            return link;
        }

        private void Open_(string url)
        {
            try
            {
                Process.Start(url);
            }
            catch (Win32Exception)
            {
                MessageBox.Show(this, "Nothing on this machine would open " + url + ".",
                    _about.Name, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private static Label Dot()
        {
            return new Label
            {
                Text = "·",
                ForeColor = SystemColors.GrayText,
                AutoSize = true,
                Margin = new Padding(5, 0, 5, 0)
            };
        }

        private static Control Rule()
        {
            return new Panel
            {
                Height = 1,
                Width = ContentWidth,
                BackColor = SystemColors.ControlLight,
                Margin = new Padding(0, 10, 0, 10)
            };
        }
    }
}
