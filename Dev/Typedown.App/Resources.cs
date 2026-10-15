using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;

namespace Typedown.Core
{
    public class Resources : ResourceDictionary
    {
        public Resources()
        {
            MergedDictionaries.Add(new Microsoft.UI.Xaml.Controls.XamlControlsResources());
            // Core was merged into this app assembly, so ms-appx paths no longer carry
            // the old "Typedown.Core" assembly component.
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Resources/Styles/Button.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Resources/Styles/ComboBox.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Resources/Styles/ContentDialog.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Resources/Styles/ToggleSwitch.xaml") });
            MergedDictionaries.Add(new() { Source = new("ms-appx:///Resources/Converters.xaml") });
        }
    }
}
