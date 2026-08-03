using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tijori.Services
{
    public static class LoadingService
    {
        // Event channel fired when the status state toggles
        public static event Action<bool, string>? OnLoadingStateChanged;

        /// <summary>
        /// Displays the global full-screen loading spinner with a custom message.
        /// Usage: LoadingService.Show("Fetching leads data...");
        /// </summary>
        public static void Show(string statusMessage = "Loading, please wait...")
        {
            OnLoadingStateChanged?.Invoke(true, statusMessage);
        }

        /// <summary>
        /// Dismisses the loading animation overlay cleanly.
        /// Usage: LoadingService.Hide();
        /// </summary>
        public static void Hide()
        {
            OnLoadingStateChanged?.Invoke(false, string.Empty);
        }
    }
}
