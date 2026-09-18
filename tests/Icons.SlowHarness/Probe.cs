using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Icons.Ui;

namespace Icons.SlowHarness
{
    /// <summary>
    /// The control from the outside, which is where a user is. Everything a scenario presses is
    /// pressed for real - PerformClick, a row picked, text typed - and everything it reads is
    /// what is on screen at that moment.
    ///
    /// It gets there through reflection, the same trade the UI harness makes: the fields are
    /// private because nothing outside the control has any business with them, and widening
    /// them for a test bench would be a worse thing to do to the tool than this is.
    /// </summary>
    public class Probe
    {
        private const BindingFlags Priv = BindingFlags.NonPublic | BindingFlags.Instance;

        private readonly IconsControl _control;

        public Probe(IconsControl control)
        {
            _control = control;
        }

        public T Field<T>(string name)
        {
            var field = typeof(IconsControl).GetField(name, Priv);
            if (field == null) throw new MissingFieldException("IconsControl", name);
            return (T)field.GetValue(_control);
        }

        // ===== what is on screen =====

        // Buttons on the form rather than items on the toolbar: the reload sits beside the
        // table list it fills, and the apply at the foot of the form that fills it in.
        public Button Load { get { return Field<Button>("_loadButton"); } }
        public Button Apply { get { return Field<Button>("_applyButton"); } }
        public ToolStripButton Cancel { get { return Field<ToolStripButton>("_cancelButton"); } }
        public ToolStripButton Undo { get { return Field<ToolStripButton>("_undoButton"); } }
        public ToolStripButton Redo { get { return Field<ToolStripButton>("_redoButton"); } }

        public string Status { get { return Field<ToolStripLabel>("_statusLabel").Text; } }

        /// <summary>
        /// Every step of the last write, kept on the status line's tooltip now that finishing one
        /// no longer puts up a dialog. Empty when nothing has been written.
        /// </summary>
        public string StatusDetail { get { return Field<ToolStripLabel>("_statusLabel").ToolTipText ?? ""; } }

        /// <summary>The red line at the bottom, or null while it is hidden.</summary>
        public string Trouble
        {
            get
            {
                var label = Field<Label>("_troubleLabel");
                return label.Visible ? label.Text : null;
            }
        }

        /// <summary>The orange line above the form, or null while it is hidden.</summary>
        public string Warning
        {
            get
            {
                var label = Field<Label>("_warningLabel");
                return label.Visible ? label.Text : null;
            }
        }

        /// <summary>The icon in hand, by its name in its family, or null when there is none.</summary>
        public string SelectedIcon
        {
            get
            {
                var icon = Field<object>("_selectedIcon");
                return icon == null ? null : (string)icon.GetType().GetProperty("Name").GetValue(icon, null);
            }
        }
        /// <summary>The weight of the icon in hand, in its family's own word for it.</summary>
        public string SelectedWeight
        {
            get
            {
                var icon = Field<object>("_selectedIcon");
                return icon == null ? null : (string)icon.GetType().GetProperty("Weight").GetValue(icon, null);
            }
        }

        /// <summary>Which family buttons are lit, by their captions. None lit means no filter.</summary>
        public string[] LitFamilies()
        {
            return Field<List<CheckBox>>("_familyButtons")
                .Where(b => b.Checked)
                .Select(b => b.Text)
                .ToArray();
        }

        /// <summary>Clicks the link in the line saying what the table is carrying.</summary>
        public void FollowCarryingLink()
        {
            var label = Field<LinkLabel>("_currentLabel");
            if (label.Links.Count == 0 || label.LinkArea.Length == 0) return;

            typeof(LinkLabel)
                .GetMethod("OnLinkClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(label, new object[] { new LinkLabelLinkClickedEventArgs(label.Links[0]) });
        }
        /// <summary>What the window says the chosen table is already carrying, or null when it says nothing.</summary>
        public string Carrying
        {
            get
            {
                var label = Field<LinkLabel>("_currentLabel");
                return label.Visible && label.Text.Length > 0 ? label.Text : null;
            }
        }
        public ListBox Tables { get { return Field<ListBox>("_tableBox"); } }
        public TextBox TableFilter { get { return Field<TextBox>("_tableFilterBox"); } }
        public CheckBox UnsetOnly { get { return Field<CheckBox>("_unsetOnlyBox"); } }
        public CheckBox HideLocked { get { return Field<CheckBox>("_hideLockedBox"); } }
        public CheckBox Raster { get { return Field<CheckBox>("_rasterBox"); } }
        public ComboBox Solutions { get { return Field<ComboBox>("_solutionBox"); } }
        public TextBox Name { get { return Field<TextBox>("_nameBox"); } }
        public TextBox Search { get { return Field<TextBox>("_searchBox"); } }
        /// <summary>The weight toggles, lit or not: outline first, solid second.</summary>
        public void ShowWeights(bool outline, bool solid)
        {
            typeof(IconsControl)
                .GetMethod("ShowWeights", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(_control, new object[] { outline, solid });
        }
        public ListView Grid { get { return Field<ListView>("_grid"); } }

        /// <summary>What the in-context preview is saying about the navigation it is drawing.</summary>
        public string ContextNote
        {
            get
            {
                var preview = Field<ContextPreview>("_context");
                if (preview == null) return "";

                var note = preview.Controls.Cast<Control>().FirstOrDefault(c => c.Name == "note");
                return note == null ? "" : note.Text;
            }
        }

        /// <summary>
        /// What the tool thinks is outstanding. Read directly rather than inferred from the
        /// buttons, because "the flag came back up" has to be checkable apart from whatever
        /// SyncEnabled did with it.
        /// </summary>
        public bool LoadingTables { get { return Field<bool>("_loadingTables"); } }
        public bool LoadingSolutions { get { return Field<bool>("_loadingSolutions"); } }
        public bool Applying { get { return Field<bool>("_applying"); } }

        /// <summary>
        /// The unique name and publisher prefix of the solution the box is showing, or null when
        /// it is showing nothing. The row's caption is a friendly name and a prefix; what a write
        /// actually goes through is neither, so it is read off the item itself.
        /// </summary>
        public string SelectedSolution { get { return SolutionField("UniqueName"); } }

        public string SelectedPrefix { get { return SolutionField("Prefix"); } }

        private string SolutionField(string name)
        {
            var item = Solutions.SelectedItem;
            if (item == null) return null;

            var field = item.GetType().GetField(name);
            if (field == null) throw new MissingFieldException("SolutionItem", name);
            return (string)field.GetValue(item);
        }

        public string[] SolutionNames()
        {
            return Solutions.Items.Cast<object>().Select(i => i.ToString()).ToArray();
        }

        public string[] TableNames()
        {
            return Tables.Items.Cast<object>().Select(i => i.ToString()).ToArray();
        }

        // ===== what a user does =====

        public void PressLoad() { Load.PerformClick(); }

        public void PressApply() { Apply.PerformClick(); }

        public void PressCancel() { Cancel.PerformClick(); }

        public void PressUndo() { Undo.PerformClick(); }

        public void PressRedo() { Redo.PerformClick(); }

        /// <summary>
        /// A keystroke as the window itself sees it. Sent to ProcessCmdKey rather than through
        /// the message queue, for the same reason Enter is: a harness window is not the focused
        /// one, and a key that lands nowhere would pass by proving nothing.
        /// </summary>
        public bool SendKey(Keys keys)
        {
            var method = typeof(IconsControl).GetMethod("ProcessCmdKey", Priv);
            if (method == null) throw new MissingMethodException("IconsControl", "ProcessCmdKey");

            var args = new object[] { new Message(), keys };
            return (bool)method.Invoke(_control, args);
        }

        /// <summary>
        /// Enter, in the search box. Sent to the handler rather than through the message queue:
        /// a harness window is not always the focused one, and a keystroke that lands nowhere
        /// would pass this by proving nothing.
        /// </summary>
        public void PressEnterInSearch()
        {
            var handler = typeof(IconsControl).GetMethod("Search_KeyDown", Priv);
            if (handler == null) throw new MissingMethodException("IconsControl", "Search_KeyDown");
            handler.Invoke(_control, new object[] { Search, new KeyEventArgs(Keys.Enter) });
        }

        /// <summary>Picks the first icon of the grid, exactly as a click on its cell would.</summary>
        public void PickFirstIcon()
        {
            if (Grid.VirtualListSize == 0) throw new InvalidOperationException("The grid is empty.");
            Grid.SelectedIndices.Clear();
            Grid.SelectedIndices.Add(0);
        }

        /// <summary>Picks the table whose entry mentions the given name.</summary>
        public void PickTable(string logicalName)
        {
            var index = Tables.Items.Cast<object>().ToList()
                .FindIndex(i => i.ToString().Contains("(" + logicalName + ")"));
            if (index < 0)
                throw new InvalidOperationException("No table called " + logicalName + " in the list: "
                                                    + string.Join(", ", TableNames()));
            Tables.SelectedIndex = index;
        }
    }
}
