using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace DeafDirectionalHelper.View;

public partial class ProfileEditorWindow : ThemedDialog
{
    /// <summary>
    /// The profile name entered by the user.
    /// </summary>
    public string ProfileName { get; private set; } = "";

    /// <summary>
    /// The executable path selected by the user.
    /// </summary>
    public string? ExePath { get; private set; }

    /// <summary>
    /// Extra process names (without .exe) that also activate this profile -
    /// anti-cheat games run under a different exe than their launcher.
    /// </summary>
    public List<string> AdditionalProcessNames { get; private set; } = new();

    /// <summary>
    /// Whether this is editing the Default profile (exe path disabled).
    /// </summary>
    public bool IsDefaultProfile { get; set; }

    /// <summary>
    /// Whether this is creating a new profile (vs editing existing).
    /// </summary>
    public bool IsNewProfile { get; set; }

    public ProfileEditorWindow()
    {
        InitializeComponent();
        Loaded += (_, _) => ProfileNameTextBox.Focus();
    }

    /// <summary>
    /// Sets the initial values for editing an existing profile.
    /// </summary>
    public void SetProfile(string name, string? exePath, bool isDefault,
        IEnumerable<string>? additionalProcessNames = null)
    {
        ProfileName = name;
        ExePath = exePath;
        IsDefaultProfile = isDefault;
        AdditionalProcessNames = additionalProcessNames?.ToList() ?? new List<string>();
        RebuildAdditionalList();

        ProfileNameTextBox.Text = name;
        ExePathTextBox.Text = exePath ?? "";

        if (isDefault)
        {
            ExePathTextBox.IsEnabled = false;
            AdditionalSection.Visibility = Visibility.Collapsed;
            InfoText.Text = "The Default profile is used when no other profiled game is running.";
            Title = "Edit Default profile";
            OkButton.Content = "Save";
        }
        else if (IsNewProfile)
        {
            Title = "New profile";
            OkButton.Content = "Create profile";
            UpdateHelperText();
        }
        else
        {
            Title = "Edit profile";
            OkButton.Content = "Save";
            UpdateHelperText();
        }
    }

    private void UpdateHelperText()
    {
        var exe = string.IsNullOrEmpty(ExePath)
            ? null
            : System.IO.Path.GetFileName(ExePath);
        InfoText.Text = exe != null
            ? $"When {exe} is running, this profile switches on automatically."
            : "Pick the game's .exe and this profile will switch on automatically whenever it runs.";
    }

    private void Browse_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select Game or Application",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() == true)
        {
            ExePath = dialog.FileName;
            ExePathTextBox.Text = dialog.FileName;
            UpdateHelperText();

            // Auto-fill name if empty or still default
            if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text) ||
                ProfileNameTextBox.Text == "New Profile")
            {
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                ProfileNameTextBox.Text = fileName;
            }
        }
    }

    private void AddAdditional_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select the game's other executable",
            Filter = "Executables (*.exe)|*.exe|All Files (*.*)|*.*",
            FilterIndex = 1
        };

        if (dialog.ShowDialog() != true)
            return;

        var name = Path.GetFileNameWithoutExtension(dialog.FileName);
        var primary = string.IsNullOrEmpty(ExePath) ? null : Path.GetFileNameWithoutExtension(ExePath);

        if (string.Equals(name, primary, StringComparison.OrdinalIgnoreCase) ||
            AdditionalProcessNames.Contains(name, StringComparer.OrdinalIgnoreCase))
            return; // already matches

        AdditionalProcessNames.Add(name);
        RebuildAdditionalList();
    }

    private void RebuildAdditionalList()
    {
        AdditionalList.Children.Clear();

        foreach (var name in AdditionalProcessNames)
        {
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            row.Children.Add(new TextBlock
            {
                Text = $"{name}.exe",
                FontFamily = new FontFamily("Consolas"),
                FontSize = 13,
                Foreground = (Brush)FindResource("TextSecondary"),
                VerticalAlignment = VerticalAlignment.Center
            });

            var remove = new Button
            {
                Content = "Remove",
                Style = (Style)FindResource("DangerLinkButton"),
                Foreground = (Brush)FindResource("DangerText"),
                FontSize = 12.5
            };
            var captured = name;
            remove.Click += (_, _) =>
            {
                AdditionalProcessNames.RemoveAll(n =>
                    string.Equals(n, captured, StringComparison.OrdinalIgnoreCase));
                RebuildAdditionalList();
            };
            Grid.SetColumn(remove, 1);
            row.Children.Add(remove);

            AdditionalList.Children.Add(row);
        }
    }

    private void OK_Click(object sender, RoutedEventArgs e)
    {
        ProfileName = ProfileNameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(ProfileName))
        {
            ThemedMessageBox.Show("Please enter a profile name.", "Validation Error", this);
            ProfileNameTextBox.Focus();
            return;
        }

        if (!IsDefaultProfile && string.IsNullOrWhiteSpace(ExePath))
        {
            ThemedMessageBox.Show("Please select an executable for this profile.", "Validation Error", this);
            return;
        }

        DialogResult = true;
        Close();
    }
}
