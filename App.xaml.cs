using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace DeafDirectionalHelper
{
    /// <summary>
    /// DeafDirectionalHelper - Accessibility Tool for Deaf and Hard-of-Hearing Gamers
    ///
    /// IMPORTANT ACCESSIBILITY NOTICE:
    /// ===============================
    /// This application is an ACCESSIBILITY TOOL designed to help deaf and hard-of-hearing
    /// players perceive directional audio cues that hearing players naturally receive.
    ///
    /// HOW IT WORKS:
    /// - Reads audio OUTPUT levels from Windows audio APIs (NAudio/WASAPI)
    /// - Monitors what your sound card is sending to your speakers/headphones
    /// - Displays visual indicators showing which direction sounds are coming from
    ///
    /// WHAT THIS TOOL DOES NOT DO:
    /// - Does NOT read, modify, or interact with any game memory or processes
    /// - Does NOT intercept or collect any game data
    /// - Does NOT modify game files or bypass any protections
    /// - Does NOT provide automation, aim assist, or any gameplay advantage
    /// - Does NOT inject code into any application
    ///
    /// This tool provides ACCESSIBILITY PARITY - it visualizes audio information that
    /// hearing players already perceive naturally through their ears. It does not provide
    /// any information that isn't already available to hearing players.
    ///
    /// The approach is similar to:
    /// - A deaf person using a flashing doorbell light
    /// - Bass shakers that convert audio to vibration
    /// - LED strips that react to music/audio output
    ///
    /// For more information, see ACCESSIBILITY.md in the project root.
    /// </summary>
    public partial class App : Application
    {
    }
}