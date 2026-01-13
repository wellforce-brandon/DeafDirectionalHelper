using System.IO;
using System.Windows;
using Microsoft.Win32;

namespace DeafDirectionalHelper.View;

public partial class ProfileEditorWindow : Window
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
    }

    /// <summary>
    /// Sets the initial values for editing an existing profile.
    /// </summary>
    public void SetProfile(string name, string? exePath, bool isDefault)
    {
        ProfileName = name;
        ExePath = exePath;
        IsDefaultProfile = isDefault;

        ProfileNameTextBox.Text = name;
        ExePathTextBox.Text = exePath ?? "";

        if (isDefault)
        {
            ExePathTextBox.IsEnabled = false;
            InfoText.Text = "The Default profile is used when no other profiled application is running.";
            Title = "Edit Default Profile";
        }
        else if (IsNewProfile)
        {
            Title = "New Profile";
            InfoText.Text = "Select an executable to automatically switch to this profile when it's running.";
        }
        else
        {
            Title = "Edit Profile";
            InfoText.Text = "Change the profile name or select a different executable.";
        }
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

            // Auto-fill name if empty or still default
            if (string.IsNullOrWhiteSpace(ProfileNameTextBox.Text) ||
                ProfileNameTextBox.Text == "New Profile")
            {
                var fileName = Path.GetFileNameWithoutExtension(dialog.FileName);
                ProfileNameTextBox.Text = fileName;
            }
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

    private void Cancel_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
