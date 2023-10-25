#if IOS || ANDROID
using Microsoft.Maui.Controls.Platform;
using Microsoft.Maui.Graphics.Platform;
#endif
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
#if IOS
using UIKit;
#endif

namespace Label_Measurement_Bug {
    public partial class App : Application {
        public App() {
            InitializeComponent();

            ContentPage mainPage = new();
            MainPage = mainPage;

            AbsoluteLayout abs = new();
            mainPage.Content = abs;

            AbsoluteLayout absFullScreen = new();
            abs.Add(absFullScreen);

            mainPage.SizeChanged += delegate {
                if (mainPage.Width > 0) {
                    absFullScreen.WidthRequest = mainPage.Width;
                    absFullScreen.HeightRequest = mainPage.Height;
                }
            };
            mainPage.HandlerChanged += delegate {
                LabelMeasurer.Instance.setFontManager(mainPage.Handler.MauiContext);
            };

            //=================
            //LABEL PARAMS
            //=================
            LabelDisplayParams labelParams = new();
            labelParams.text = "HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO HELLO ";
            labelParams.fontSize = 14;
            labelParams.fontName = "MontserratExtraBold";
            labelParams.lineSpacing = 1;
            labelParams.maxWidth = 30;
            labelParams.wrap = LineBreakMode.WordWrap;

            //===========
            //LABEL
            //===========
            Label label = new();
            label.Text = labelParams.text;
            label.FontSize = labelParams.fontSize;
            label.MaximumWidthRequest = labelParams.maxWidth;
            label.LineHeight = labelParams.lineSpacing;
            label.LineBreakMode = LineBreakMode.WordWrap;
            label.FontFamily = labelParams.fontName;
            absFullScreen.Add(label);

            //===================
            //DEBUG OUTPUT
            //===================
            IDispatcherTimer timer = Application.Current.Dispatcher.CreateTimer();
            timer.Start();
            timer.Interval = TimeSpan.FromSeconds(1f);
            timer.Tick += delegate {

                Size trueSize = label.Measure(1000, 1000, MeasureFlags.None);
                Size measureSize = LabelMeasurer.Instance.getMeasurement(labelParams);
                Debug.WriteLine("True Size " + trueSize + " Measured " + measureSize);

            };
        }
    }

    public class LabelDisplayParams {

        public string text = null;
        public double fontSize = 0;
        public string fontName = ""; 
        public LineBreakMode wrap = LineBreakMode.WordWrap;
        public double lineSpacing = 1;
        public int maxWidth = 5000; //note must be an int in android - best to make int here too then
    }
    public class LabelMeasurer {

        //===================
        //MAKE LAZY SINGLETON
        //===================
        IFontManager fontManager = null;
        public static LabelMeasurer Instance { get { return lazy.Value; } }
        private static readonly Lazy<LabelMeasurer> lazy = new Lazy<LabelMeasurer>(() => new LabelMeasurer());
        public void setFontManager(IMauiContext mauiContext) {
            if (fontManager == null) {
                fontManager = mauiContext.Services.GetRequiredService<IFontManager>();
            }
        }
        public Size getMeasurement(LabelDisplayParams labelParams) {
            Size size = new();

            if (fontManager != null) {
                
                var font = Microsoft.Maui.Font.OfSize(labelParams.fontName, labelParams.fontSize);

#if WINDOWS

                //===================================
                //THIS WORKS PERFECTLY IN WINDOWS
                //===================================
                Microsoft.UI.Xaml.Controls.TextBlock newTB = new();
                newTB.Text = labelParams.text;
                newTB.FontSize = labelParams.fontSize;
                newTB.MaxWidth = labelParams.maxWidth;

                newTB.FontFamily = fontManager.GetFontFamily(font);
                //newTB.ApplyFont(font, fontManager);
                newTB.TextWrapping = Microsoft.UI.Xaml.TextWrapping.Wrap;
                newTB.LineHeight = labelParams.lineSpacing; //i think this is correct
                newTB.Padding = new Microsoft.UI.Xaml.Thickness(0);
                newTB.Margin = new Microsoft.UI.Xaml.Thickness(0);
                newTB.Measure(new Windows.Foundation.Size(Double.PositiveInfinity, Double.PositiveInfinity)); //updates it internally

                Windows.Foundation.Size result = (newTB.DesiredSize);
                size = new Size(result.Width, result.Height); 
                return size;

                //returns: True Size { Width = 30 Height = 581} Measured { Width = 30 Height = 581} //measuring perfectly
#endif
#if ANDROID
                //================================================
                //OBSOLETE WAY TO MEASURE - GIVING BAD RESULTS
                //================================================
                //used also by maui here: https://github.com/dotnet/maui/blob/783db2459c6e3ec6437f9939a0ea2681fd634a3d/src/Graphics/src/Graphics/Platforms/Android/PlatformStringSizeService.cs
                
                //make text paint
                Android.Text.TextPaint textPaint = new();
                textPaint.TextSize = (float)labelParams.fontSize;
                textPaint.SetTypeface(font.ToTypeface(fontManager));

                //get java string
                Java.Lang.StringBuilder javaString = new Java.Lang.StringBuilder();
                javaString.Append(labelParams.text);

                //make static layout
                var staticLayout = new Android.Text.StaticLayout(javaString, textPaint, labelParams.maxWidth, Android.Text.Layout.Alignment.AlignNormal, 1, 0, true);
                var sizeF = staticLayout.GetTextSizeAsSizeF(false);
                size = new(sizeF.Width, sizeF.Height);
                staticLayout.Dispose();


                //==========================================================
                //CORRECT NEWER WAY TO MEASURE BUT DOESN'T WORK IN MAUI
                //==========================================================
                //new way to make builder suggested //https://developer.android.com/reference/android/text/StaticLayout
                //Android.Text.StaticLayout.Builder builder = Android.Text.StaticLayout.Builder.Obtain(cs, 0, cs.Length() - 1, textPaint, (int)labelParams.maxWidth);
                //builder.SetText(javaString);
                //builder.SetIncludePad(false); //Set whether to include extra space beyond font ascent and descent (which is needed to avoid clipping in some languages, such as Arabic and Kannada). 
                //builder.SetLineBreakConfig(Android.Graphics.Text.LineBreakStyle.Normal); //BROKEN || see: https://developer.android.com/reference/android/graphics/text/LineBreakConfig
                //builder.SetLineBreakConfig(Android.Graphics.Text.LineBreakConfig.LineBreakStyleNormal); //BROKEN || see: https://developer.android.com/reference/android/graphics/text/LineBreakConfig
                //Android.Text.StaticLayout layout = builder.Build(); //Note: the builder object must not be reused in any way after calling this method. Setting parameters after calling this method, or calling it a second time on the same builder object, will likely lead to unexpected results.
                //size = new(layout.Width, layout.Height);

                return size;

                //Returns: [0:] True Size {Width=30.181818181818183 Height=583.2727272727273} Measured {Width=29 Height=614} // very wrong
#endif

#if IOS

                //=======================================================
                //WORKING REASONABLY WELL IN IOS - STILL SLIGHTLY OFF
                //=======================================================
                //from  https://github.com/dotnet/maui/blob/783db2459c6e3ec6437f9939a0ea2681fd634a3d/src/Graphics/src/Graphics/Platforms/iOS/PlatformStringSizeService.cs#L7

                Foundation.NSString nsString = new Foundation.NSString(labelParams.text);
                UIKit.UIFont uiFont = font.ToUIFont(fontManager);
                uiFont.WithSize((System.Runtime.InteropServices.NFloat)labelParams.fontSize);

                CoreGraphics.CGSize constraintSize = new(labelParams.maxWidth, 90000); 
                CoreGraphics.CGSize cgSizeResult; 

                if (!UIDevice.CurrentDevice.CheckSystemVersion(14, 0)) {
                    //=====================================
                    //I HAVE NOT TESTED THIS OLDER METHOD:
                    //=====================================

                    cgSizeResult = nsString.GetBoundingRect(
                        constraintSize,
                        Foundation.NSStringDrawingOptions.UsesLineFragmentOrigin,
                        new UIStringAttributes { Font = uiFont },
                        null).Size;
                }
                else {
                    //==============================
                    //CURRENT USAGE METHOD TESTED
                    //==============================
                    cgSizeResult = UIKit.UIStringDrawing.StringSize(nsString, uiFont, constraintSize, UILineBreakMode.WordWrap);
                }
                size = new Size(cgSizeResult.Width, cgSizeResult.Height);
                nsString.Dispose();
                //uiFont.Dispose(); //CAN'T RUN THIS - TRIGGERS  Terminating app due to uncaught exception 'System.ObjectDisposedException', reason: 'Cannot access a disposed object. Object name: 'UIKit.UIFont'. (System.ObjectDisposedException)
                return size;

                //returns: [0:] True Size { Width = 29.333333333333332 Height = 580.3333333333334} Measured { Width = 29.232 Height = 580.2439999999997}
#endif

            }
            return size;
        }
    }
}