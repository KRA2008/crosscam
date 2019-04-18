## CrossCam is a cross-platform what-you-see-is-what-you-get stereoscopic 3D cross view camera mobile app.

iOS: https://itunes.apple.com/app/id1436262905

Android: https://play.google.com/store/apps/details?id=com.kra2008.crosscam

---

### Acknowledgements --- Special thanks to:

SkiaSharp for the bitmap image editing: https://github.com/mono/SkiaSharp

OpenCV for the automatic alignment: https://opencv.org/

Emgu CV for the C#/Xamarin wrappers for OpenCV: http://www.emgu.com

Satya Mallick for this blog post that led me to OpenCV: https://www.learnopencv.com/image-alignment-ecc-in-opencv-c-python/

Testers on the CrossView subreddit: https://old.reddit.com/r/CrossView/

---

### How to contribute:

Just do something and send it my way. Thanks.

#### Things you need:
Visual Studio 2017 with Xamarin

#### How to build:
##### Android:
    StartUp Project: CrossCam.Droid
    Configuration: Debug Without Emgu
    Platform: Any CPU

##### iOS:
    StartUp Project: CrossCam.iOS
    Configuration: Debug Without Emgu
    Platform: iPhone

If you get build errors related to the missing Emgu dependency, check your configuration again. The EmguCV binaries are not included in source control because to do so would violate the terms of the license agreement.
