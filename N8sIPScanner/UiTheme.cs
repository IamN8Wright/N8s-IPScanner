using System;
using System.Drawing;
using System.Windows.Forms;

namespace N8sIPScanner;

public static class UiTheme
{
    public static string Mode { get; private set; } = "Dark";

    public static bool IsDark => string.Equals(Mode, "Dark", StringComparison.OrdinalIgnoreCase);

    public static Color Background => IsDark ? Color.FromArgb(13, 18, 30) : Color.FromArgb(241, 245, 249);
    public static Color Panel => IsDark ? Color.FromArgb(24, 31, 48) : Color.FromArgb(255, 255, 255);
    public static Color PanelAlt => IsDark ? Color.FromArgb(31, 41, 59) : Color.FromArgb(226, 232, 240);
    public static Color Text => IsDark ? Color.FromArgb(226, 232, 240) : Color.FromArgb(15, 23, 42);
    public static Color MutedText => IsDark ? Color.FromArgb(148, 163, 184) : Color.FromArgb(71, 85, 105);
    public static Color Accent => Color.FromArgb(0, 71, 171);
    public static Color AccentBright => IsDark ? Color.FromArgb(37, 99, 235) : Color.FromArgb(29, 78, 216);
    public static Color Field => IsDark ? Color.FromArgb(15, 23, 42) : Color.FromArgb(248, 250, 252);
    public static Color Border => IsDark ? Color.FromArgb(71, 85, 105) : Color.FromArgb(148, 163, 184);
    public static Color Success => IsDark ? Color.FromArgb(74, 222, 128) : Color.FromArgb(22, 101, 52);
    public static Color Warning => IsDark ? Color.FromArgb(251, 191, 36) : Color.FromArgb(146, 64, 14);
    public static Color ButtonText => Color.White;

    public static void SetMode(string mode)
    {
        Mode = string.Equals(mode, "Light", StringComparison.OrdinalIgnoreCase) ? "Light" : "Dark";
    }

    public static void Apply(Form form)
    {
        form.BackColor = Background;
        form.ForeColor = Text;
        ApplyToChildren(form);
    }

    public static void ApplyToChildren(Control parent)
    {
        foreach (Control control in parent.Controls)
        {
            ApplyControl(control);

            if (control.HasChildren)
            {
                ApplyToChildren(control);
            }
        }
    }

    public static void ApplyControl(Control control)
    {
        switch (control)
        {
            case GroupBox groupBox:
                groupBox.BackColor = Panel;
                groupBox.ForeColor = Text;
                break;

            case Label label:
                label.BackColor = Color.Transparent;
                label.ForeColor = Text;
                break;

            case Button button:
                button.FlatStyle = FlatStyle.Flat;
                button.FlatAppearance.BorderColor = AccentBright;
                button.FlatAppearance.MouseOverBackColor = IsDark ? Color.FromArgb(29, 78, 216) : Color.FromArgb(37, 99, 235);
                button.FlatAppearance.MouseDownBackColor = IsDark ? Color.FromArgb(30, 64, 175) : Color.FromArgb(30, 64, 175);
                button.BackColor = Accent;
                button.ForeColor = ButtonText;
                button.UseVisualStyleBackColor = false;
                break;

            case TextBox textBox:
                textBox.BackColor = Field;
                textBox.ForeColor = Text;
                textBox.BorderStyle = BorderStyle.FixedSingle;
                break;

            case ComboBox comboBox:
                comboBox.BackColor = Field;
                comboBox.ForeColor = Text;
                comboBox.FlatStyle = FlatStyle.Flat;
                break;

            case ListView listView:
                listView.BackColor = Field;
                listView.ForeColor = Text;
                listView.BorderStyle = BorderStyle.FixedSingle;
                listView.GridLines = false;
                break;

            case CheckBox checkBox:
                checkBox.BackColor = Color.Transparent;
                checkBox.ForeColor = Text;
                break;

            case RadioButton radioButton:
                radioButton.BackColor = Color.Transparent;
                radioButton.ForeColor = Text;
                break;

            case PictureBox pictureBox:
                pictureBox.BackColor = Color.Transparent;
                break;

            case AnimatedCobaltProgressBar progressBar:
                progressBar.BackColor = Field;
                break;

            case Panel panel:
                panel.BackColor = Background;
                panel.ForeColor = Text;
                break;
        }
    }

    public static void StyleSecondary(Button button)
    {
        button.BackColor = PanelAlt;
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Border;
        button.FlatAppearance.MouseOverBackColor = IsDark ? Color.FromArgb(51, 65, 85) : Color.FromArgb(203, 213, 225);
        button.FlatAppearance.MouseDownBackColor = IsDark ? Color.FromArgb(30, 41, 59) : Color.FromArgb(186, 198, 214);
        button.ForeColor = Text;
        button.UseVisualStyleBackColor = false;
    }

    public static void StyleDanger(Button button)
    {
        button.BackColor = IsDark ? Color.FromArgb(127, 29, 29) : Color.FromArgb(220, 38, 38);
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderColor = Color.FromArgb(239, 68, 68);
        button.FlatAppearance.MouseOverBackColor = IsDark ? Color.FromArgb(153, 27, 27) : Color.FromArgb(185, 28, 28);
        button.FlatAppearance.MouseDownBackColor = IsDark ? Color.FromArgb(91, 22, 22) : Color.FromArgb(153, 27, 27);
        button.ForeColor = Color.White;
        button.UseVisualStyleBackColor = false;
    }
}
