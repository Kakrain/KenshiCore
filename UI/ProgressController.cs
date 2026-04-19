using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.UI
{
    public sealed class ProgressController
    {
        private static ProgressController? _instance;
        public static ProgressController Instance => _instance ??= new ProgressController();

        private int _value;
        private int _max;
        private string _label = "";

        private ProgressBar? _bar;
        private Label? _labelCtrl;
        private System.Windows.Forms.Timer? _timer;

        private ProgressController() { }

        public void Build(Control parent)
        {
            _bar = new ProgressBar
            {
                Dock = DockStyle.Fill,
                Minimum = 0,
                Maximum = 1
            };

            _labelCtrl = new Label
            {
                Dock = DockStyle.Fill,
                Text = "Ready"
            };

            parent.Controls.Add(_bar);
            parent.Controls.Add(_labelCtrl);

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 33; // ~30fps
            _timer.Tick += (_, _) => UpdateUI();
            _timer.Start();
        }
        public void setLabel(string text)
        {
            _label = text;
        }
        public void Initialize(int max)
        {
            _max = Math.Max(1, max);
            _value = 0;
        }

        // absolute
        public void Report(int value, string? text = null)
        {
            _value = value;
            if (text != null)
                _label = text;
        }

        // increment
        public void ReportStep(string? text = null)
        {
            _value++;
            if (text != null)
                _label = text;
        }

        public void Finish()
        {
            _value = _max;
            _label = "Ready";
        }

        private void UpdateUI()
        {
            if (_bar == null || _labelCtrl == null)
                return;

            _bar.Maximum = _max;
            _bar.Value = Math.Min(_value, _max);
            _labelCtrl.Text = _label;
        }
    }

}
