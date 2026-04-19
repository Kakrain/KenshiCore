using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KenshiCore.UI
{
    public static class ThemeManager
    {
        // Default theme
        public static AppTheme Current { get; private set; } = new AppTheme
        {
            Background = Color.AliceBlue,//Color.FromArgb(unchecked((int)0xFF2F2A24)),
            Secondary = Color.Azure,//FromArgb(unchecked((int)0xFF4C433A)),
            Foreground = Color.Aquamarine//FromArgb(unchecked((int)0xFFE9E4D7))
        };
        // Set the current theme
        public static void Set(AppTheme theme)
        {
            Current = theme;
        }
        public static void ApplyTheme(Form form)
        {
            form.BackColor = Current.Background;
            form.ForeColor = Current.Foreground;

            ApplyToControls(form.Controls);
        }
        private static void ApplyToControls(Control.ControlCollection controls)
        {
            foreach (Control control in controls) { 
                ApplyThemeToControl(control);
            }
        }
        // Apply the current theme to a given control (recursively for all child controls)
        public static void ApplyThemeToControl(Control control)
        {
            if (control is Button btn)
            {
                btn.BackColor = Current.Secondary;
                btn.ForeColor = Current.Foreground;
                btn.FlatStyle = FlatStyle.Flat;
                btn.FlatAppearance.BorderSize = 0;
            }
            else if (control is Label lbl)
            {
                lbl.ForeColor = Current.Foreground;
            }
            else if (control is TextBox tb)
            {
                tb.BackColor = Current.Background;
                tb.ForeColor = Current.Foreground;
            }
            /*else if (control is ListView lv)
            {
                lv.BackColor = Current.Background;
                lv.ForeColor = Current.Foreground;
            }*/

            // Apply to children recursively
            foreach (Control childControl in control.Controls)
            {
                ApplyThemeToControl(childControl);
            }
        }

        // Apply the theme to the entire form
        public static void ApplyThemeToForm(Form form)
        {
            form.BackColor = Current.Background;
            form.ForeColor = Current.Foreground;
            ApplyThemeToControl(form);
        }
    }

    public class AppTheme
    {
        public Color Background { get; init; }
        public Color Secondary { get; init; }
        public Color Foreground { get; init; }
    }
}
