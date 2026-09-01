using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

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

        public ToolStripButton Load { get { return Field<ToolStripButton>("_loadButton"); } }
        public ToolStripButton Apply { get { return Field<ToolStripButton>("_applyButton"); } }
        public ToolStripButton Cancel { get { return Field<ToolStripButton>("_cancelButton"); } }

        public string Status { get { return Field<ToolStripLabel>("_statusLabel").Text; } }

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

        public ComboBox Tables { get { return Field<ComboBox>("_tableBox"); } }
        public CheckBox UnsetOnly { get { return Field<CheckBox>("_unsetOnlyBox"); } }
        public CheckBox Raster { get { return Field<CheckBox>("_rasterBox"); } }
        public ComboBox Solutions { get { return Field<ComboBox>("_solutionBox"); } }
        public TextBox Name { get { return Field<TextBox>("_nameBox"); } }
        public TextBox Search { get { return Field<TextBox>("_searchBox"); } }
        public ListView Grid { get { return Field<ListView>("_grid"); } }

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
