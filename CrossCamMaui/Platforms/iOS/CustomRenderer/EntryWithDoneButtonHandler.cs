using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UIKit;

namespace CrossCam.Platforms.iOS.CustomRenderer
{
    public class EntryWithDoneButtonHandler
    {
        public static void AddDone()
        {
            Microsoft.Maui.Handlers.EntryHandler.Mapper.AppendToMapping("Done", (handler, view) =>
            {
#if IOS
                var toolbar = new UIToolbar(new RectangleF(0.0f, 0.0f, 50.0f, 44.0f));
                toolbar.BackgroundColor = UIColor.LightGray; // Set the color you prefer
                var doneButton = new UIBarButtonItem(UIBarButtonSystemItem.Done, delegate
                {
                    handler.PlatformView.ResignFirstResponder();
                });

                toolbar.Items = new UIBarButtonItem[] {
                    new UIBarButtonItem (UIBarButtonSystemItem.FlexibleSpace),
                    doneButton
                };

                handler.PlatformView.InputAccessoryView = toolbar;
#endif
            });
        }
    }
}
