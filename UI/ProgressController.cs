using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.UI
{
    public interface IProgressReporter
    {
        void Initialize(int max);
        void Report(int value, string? message = null);
        void ReportStep(string? message = null);
        void Finish(string? message = null);

    }
    public sealed class ProgressController: IProgressReporter
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
        public static void For( int start,int end,Action<int> action, string? text = null)
        {
            int count = Math.Max(0, end - start);

            if (count == 0)
                return;

            ProgressController.Instance.Initialize(count);

            for (int i = start; i < end; i++)
            {
                action(i);
                ProgressController.Instance.Report(i - start + 1, text);
            }

            ProgressController.Instance.Finish(text);
        }

        public static void ForEach<T>(IEnumerable<T> items,Action<T> action,string? text = null)
        {
            var list = items as IReadOnlyCollection<T> ?? items.ToList();

            if (list.Count == 0)
                return;

            ProgressController.Instance.Initialize(list.Count);

            int i = 0;

            foreach (var item in list)
            {
                action(item);
                ProgressController.Instance.Report(++i, text);
            }

            ProgressController.Instance.Finish(text);
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

        public void Finish(string? text = null)
        {
            _value = _max;
            _label = text ?? "Ready";
        }
        private void UpdateUI()
        {
            if (_bar == null || _labelCtrl == null)
                return;

            int min = _bar.Minimum;
            int max = Math.Max(min, _max);

            _bar.Maximum = max;

            int value = Math.Clamp(_value, min, max);

            _bar.Value = value;

            _labelCtrl.Text = _label;
        }
    }

}
