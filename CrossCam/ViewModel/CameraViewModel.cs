using System;
using System.IO;
using System.Threading.Tasks;
using System.Web;
using CrossCam.Model;
using CrossCam.Page;
using CrossCam.Wrappers;
using FreshMvvm;
using SkiaSharp;
using Xamarin.Forms;

namespace CrossCam.ViewModel
{
    public sealed class CameraViewModel : FreshBasePageModel
    {
        public WorkflowStage WorkflowStage { get; set; }

        public SKBitmap LeftBitmap { get; set; }
        public Command RetakeLeftCommand { get; set; }
        public bool LeftCaptureSuccess { get; set; }
        
        public SKBitmap RightBitmap { get; set; }
        public Command RetakeRightCommand { get; set; }
        public bool RightCaptureSuccess { get; set; }

        public bool IsCameraVisible { get; set; }
        public byte[] CapturedImageBytes { get; set; }
        public bool CaptureSuccess { get; set; }
        public int CameraColumn { get; set; }

        public Command CapturePictureCommand { get; set; }
        public bool CapturePictureTrigger { get; set; }

        public bool MoveLeftTrigger { get; set; }
        public bool MoveRightTrigger { get; set; }

        public Command SaveCapturesCommand { get; set; }

        public Command ToggleViewModeCommand { get; set; }

        public Command ClearCapturesCommand { get; set; }

        public Command NavigateToSettingsCommand { get; set; }
        public Command NavigateToHelpCommand { get; set; }

        public Command SwapSidesCommand { get; set; }

        public Command GoToModeCommand { get; set; }
        public Command SaveEditCommand { get; set; }

        public Command ClearEditCommand { get; set; }

        public Command PromptForPermissionAndSendErrorEmailCommand { get; set; }
        public string ErrorMessage { get; set; }

        public Settings Settings { get; set; }

        public int LeftZoom { get; set; }
        public int RightZoom { get; set; }
        public Command LeftZoomIn => new Command(() =>
        {
            LeftZoom += Settings.ZoomSpeed;
        });
        public Command LeftZoomOut => new Command(() =>
        {
            if (LeftZoom - Settings.ZoomSpeed >= 0)
            {
                LeftZoom -= Settings.ZoomSpeed;
            }
        });
        public Command RightZoomIn => new Command(() =>
        {
            RightZoom += Settings.ZoomSpeed;
        });
        public Command RightZoomOut => new Command(() =>
        {
            if (RightZoom - Settings.ZoomSpeed >= 0)
            {
                RightZoom -= Settings.ZoomSpeed;
            }
        });

        public int LeftLeftCrop { get; set; }
        public int LeftRightCrop { get; set; }
        public int RightLeftCrop { get; set; }
        public int RightRightCrop { get; set; }
        public int LeftTopCrop { get; set; }
        public int LeftBottomCrop { get; set; }
        public int RightTopCrop { get; set; }
        public int RightBottomCrop { get; set; }

        public Command IncreaseInsideCrop => new Command(() =>
        {
            LeftRightCrop += Settings.CropSpeed;
            RightLeftCrop += Settings.CropSpeed;
        });
        public Command DecreaseInsideCrop => new Command(() =>
        {
            if (LeftRightCrop - Settings.CropSpeed >= 0 &&
                RightLeftCrop - Settings.CropSpeed >= 0)
            {
                LeftRightCrop -= Settings.CropSpeed;
                RightLeftCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseOutsideCrop => new Command(() =>
        {
            LeftLeftCrop += Settings.CropSpeed;
            RightRightCrop += Settings.CropSpeed;
        });
        public Command DecreaseOutsideCrop => new Command(() =>
        {
            if (LeftLeftCrop - Settings.CropSpeed >= 0 &&
                RightRightCrop - Settings.CropSpeed >= 0)
            {
                LeftLeftCrop -= Settings.CropSpeed;
                RightRightCrop -= Settings.CropSpeed;
            }
        });

        public Command IncreaseLeftCrop => new Command(() =>
        {
            LeftLeftCrop += Settings.CropSpeed;
            RightLeftCrop += Settings.CropSpeed;
        });
        public Command DecreaseLeftCrop => new Command(() =>
        {
            if (LeftLeftCrop - Settings.CropSpeed >= 0 &&
                RightLeftCrop - Settings.CropSpeed >= 0)
            {
                LeftLeftCrop -= Settings.CropSpeed;
                RightLeftCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseRightCrop => new Command(() =>
        {
            LeftRightCrop += Settings.CropSpeed;
            RightRightCrop += Settings.CropSpeed;
        });
        public Command DecreaseRightCrop => new Command(() =>
        {
            if (LeftRightCrop - Settings.CropSpeed >= 0 &&
                RightRightCrop - Settings.CropSpeed >= 0)
            {
                LeftRightCrop -= Settings.CropSpeed;
                RightRightCrop -= Settings.CropSpeed;
            }
        });

        public Command IncreaseTopCrop => new Command(() =>
        {
            LeftTopCrop += Settings.CropSpeed;
            RightTopCrop += Settings.CropSpeed;
        });
        public Command DecreaseTopCrop => new Command(() =>
        {
            if (LeftTopCrop - Settings.CropSpeed >= 0 &&
                RightTopCrop - Settings.CropSpeed > 0)
            {
                LeftTopCrop -= Settings.CropSpeed;
                RightTopCrop -= Settings.CropSpeed;
            }
        });
        public Command IncreaseBottomCrop => new Command(() =>
        {
            LeftBottomCrop += Settings.CropSpeed;
            RightBottomCrop += Settings.CropSpeed;
        });
        public Command DecreaseBottomCrop => new Command(() =>
        {
            if (LeftBottomCrop - Settings.CropSpeed >= 0 &&
                RightBottomCrop - Settings.CropSpeed > 0)
            {
                LeftBottomCrop -= Settings.CropSpeed;
                RightBottomCrop -= Settings.CropSpeed;
            }
        });

        public int VerticalAlignment { get; set; }
        public Command LeftUpRightDown => new Command(() =>
        {
            VerticalAlignment += Settings.AlignSpeed;
        });
        public Command LeftDownRightUp => new Command(() =>
        {
            VerticalAlignment -= Settings.AlignSpeed;
        });

        private const float ROTATION_MULTIPLIER = 100f;

        public float LeftRotation { get; set; }
        public float RightRotation { get; set; }
        
        public Command IncreaseLeftRotation => new Command(() =>  LeftRotation += Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command DecreaseLeftRotation => new Command(() => LeftRotation -= Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command IncreaseRightRotation => new Command(() => RightRotation += Settings.RotationSpeed / ROTATION_MULTIPLIER);
        public Command DecreaseRightRotation => new Command(() => RightRotation -= Settings.RotationSpeed / ROTATION_MULTIPLIER);

        private const float KEYSTONE_MULTIPLIER = 10000f;

        public float LeftKeystone { get; set; }
        public float RightKeystone { get; set; }

        public Command IncreaseLeftKeystone => new Command(() => LeftKeystone += Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command DecreaseLeftKeystone => new Command(() => LeftKeystone -= Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command IncreaseRightKeystone => new Command(() => RightKeystone += Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);
        public Command DecreaseRightKeystone => new Command(() => RightKeystone -= Settings.KeystoneSpeed / KEYSTONE_MULTIPLIER);

        public Command LoadPhotoCommand { get; set; }

        public bool IsViewPortrait { get; set; }
        public bool IsCaptureLeftFirst { get; set; }
        public bool WasCapturePortrait { get; set; }

        public bool FailFadeTrigger { get; set; }
        public bool SuccessFadeTrigger { get; set; }

        public bool SwitchToContinuousFocusTrigger { get; set; }

        public bool ShouldLeftLoadBeVisible => LeftBitmap == null && CameraColumn == 0 && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldRightLoadBeVisible => RightBitmap == null && CameraColumn == 1 && WorkflowStage == WorkflowStage.Capture;
        public bool IsNothingCaptured => LeftBitmap == null && RightBitmap == null;
        public bool ShouldHelpTextBeVisible => IsNothingCaptured && HelpTextColumn != CameraColumn && WorkflowStage == WorkflowStage.Capture;
        public bool ShouldLeftRetakeBeVisible => LeftBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool ShouldRightRetakeBeVisible => RightBitmap != null && (WorkflowStage == WorkflowStage.Capture || WorkflowStage == WorkflowStage.Final && DoesCaptureOrientationMatchViewOrientation);
        public bool DoesCaptureOrientationMatchViewOrientation => WasCapturePortrait == IsViewPortrait;
        public bool ShouldSettingsAndHelpBeVisible => WorkflowStage != WorkflowStage.Saving && WorkflowStage != WorkflowStage.View;
        public bool IsExactlyOnePictureTaken => LeftBitmap == null ^ RightBitmap == null;
        public bool ShouldLineGuidesBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideLinesWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.AreGuideLinesVisible;
        public bool ShouldDonutGuideBeVisible => (IsExactlyOnePictureTaken || Settings.ShowGuideDonutWithFirstCapture && WorkflowStage == WorkflowStage.Capture) && Settings.IsGuideDonutVisible;
        public bool ShouldRollGuideBeVisible => WorkflowStage == WorkflowStage.Capture && Settings.ShowRollGuide;
        public bool ShouldPitchGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowPitchGuide;
        public bool ShouldYawGuideBeVisible => IsExactlyOnePictureTaken && Settings.ShowYawGuide;
        public bool ShouldSaveEditsButtonBeVisible => WorkflowStage == WorkflowStage.Edits ||
                                                      WorkflowStage == WorkflowStage.Crop ||
                                                      WorkflowStage == WorkflowStage.Keystone ||
                                                      WorkflowStage == WorkflowStage.Align;
        public bool ShouldViewButtonBeVisible => WorkflowStage == WorkflowStage.Final ||
                                                 WorkflowStage == WorkflowStage.Crop ||
                                                 WorkflowStage == WorkflowStage.Keystone ||
                                                 WorkflowStage == WorkflowStage.Align;
        public bool ShouldClearEditButtonBeVisible => WorkflowStage == WorkflowStage.Crop ||
                                                      WorkflowStage == WorkflowStage.Keystone ||
                                                      WorkflowStage == WorkflowStage.Align;

        public string HelpText => "(flip for " + OppositeOrientation + ")" +
                                  "\n1) Frame up your subject" +
                                  "\n2) Take the first picture (but finish reading these directions first)" +
                                  "\n3) Move " + SlideDirection + "" +
                                  "\n4) Start cross viewing with the preview that will have taken the place of these instructions" +
                                  "\n5) Guide lines will have appeared, align the second picture so the guide lines and the 3D image itself appear clear and sharp" +
                                  "\n6) Take the second picture when the desired level of 3D is achieved";
        public string SlideDirection => IsCaptureLeftFirst ? "LEFT" : "RIGHT";
        public int HelpTextColumn => IsCaptureLeftFirst ? 1 : 0;
        public string OppositeOrientation => IsViewPortrait ? "landscape" : "portrait";
        public bool ShouldPortraitViewModeWarningBeVisible => WorkflowStage != WorkflowStage.Capture && WorkflowStage != WorkflowStage.Saving && IsViewPortrait;

        public ImageSource LeftReticleImage => ImageSource.FromFile("squareOuter");
        public ImageSource RightReticleImage => Settings.IsGuideDonutBothDonuts
            ? ImageSource.FromFile("squareOuter")
            : ImageSource.FromFile("squareInner");

        private bool _needs180Flip;
        private WorkflowStage _stageBeforeView;

        public CameraViewModel()
        {
            var photoSaver = DependencyService.Get<IPhotoSaver>();
            IsCameraVisible = true;

            Settings = PersistentStorage.LoadOrDefault(PersistentStorage.SETTINGS_KEY, new Settings());

            IsCaptureLeftFirst = Settings.IsCaptureLeftFirst;
            CameraColumn = IsCaptureLeftFirst ? 0 : 1;
            
            PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName == nameof(CaptureSuccess))
                {
                    if (CameraColumn == 0)
                    {
                        LeftCaptureSuccess = !LeftCaptureSuccess;
                    }
                    else
                    {
                        RightCaptureSuccess = !RightCaptureSuccess;
                    }
                }
                else if (args.PropertyName == nameof(CapturedImageBytes))
                {
                    if (CameraColumn == 0)
                    {
                        LeftBitmap = GetBitmapAndCorrectOrientation(CapturedImageBytes);
                        WasCapturePortrait = LeftBitmap.Width < LeftBitmap.Height;

                        if (RightBitmap == null)
                        {
                            MoveLeftTrigger = !MoveLeftTrigger;
                            CameraColumn = 1;
                        }
                        else
                        {
                            CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                            IsCameraVisible = false;
                            WorkflowStage = WorkflowStage.Final;
                        }
                    }
                    else
                    {
                        RightBitmap = GetBitmapAndCorrectOrientation(CapturedImageBytes);
                        WasCapturePortrait = RightBitmap.Width < RightBitmap.Height;

                        if (LeftBitmap == null)
                        {
                            MoveRightTrigger = !MoveRightTrigger;
                            CameraColumn = 0;
                        }
                        else
                        {
                            CameraColumn = IsCaptureLeftFirst ? 0 : 1;
                            IsCameraVisible = false;
                            WorkflowStage = WorkflowStage.Final;
                        }
                    }
                }
                else if (args.PropertyName == nameof(ErrorMessage))
                {
                    if (ErrorMessage != null)
                    {
                        PromptForPermissionAndSendErrorEmailCommand.Execute(null);
                    }
                }
            };

            LoadPhotoCommand = new Command(async () =>
            {
                var photo = await DependencyService.Get<IPhotoPicker>().GetImage();
                if (photo != null)
                {
                    CapturedImageBytes = photo;
                }
            });

            RetakeLeftCommand = new Command(() =>
            {
                ClearEdits();
                CameraColumn = 0;
                IsCameraVisible = true;
                LeftBitmap = null;
                if (RightBitmap != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }
                WorkflowStage = WorkflowStage.Capture;
            });

            RetakeRightCommand = new Command(() =>
            {
                ClearEdits();
                CameraColumn = 1;
                IsCameraVisible = true;
                RightBitmap = null;
                if (LeftBitmap != null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }
                WorkflowStage = WorkflowStage.Capture;
            });

            GoToModeCommand = new Command<WorkflowStage>(arg =>
            {
                WorkflowStage = arg;
            });

            SaveEditCommand = new Command(() =>
            {
                WorkflowStage = WorkflowStage == WorkflowStage.Edits ? WorkflowStage.Final : WorkflowStage.Edits;
            });

            ClearEditCommand = new Command(() =>
            {
                switch (WorkflowStage)
                {
                    case WorkflowStage.Crop:
                        ClearCrops();
                        break;
                    case WorkflowStage.Align:
                        ClearAlignments();
                        break;
                    case WorkflowStage.Keystone:
                        ClearKeystone();
                        break;
                }
            });

            ClearCapturesCommand = new Command(ClearCaptures);

            CapturePictureCommand = new Command(() =>
            {
                CapturePictureTrigger = !CapturePictureTrigger;
            });

            ToggleViewModeCommand = new Command(() =>
            {
                if (WorkflowStage != WorkflowStage.View)
                {
                    _stageBeforeView = WorkflowStage;
                    WorkflowStage = WorkflowStage.View;
                }
                else
                {
                    WorkflowStage = _stageBeforeView;
                }
            });

            NavigateToSettingsCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<SettingsViewModel>(Settings);
            });

            NavigateToHelpCommand = new Command(async () =>
            {
                await CoreMethods.PushPageModel<HelpViewModel>();
            });

            SwapSidesCommand = new Command(() =>
            {
                IsCaptureLeftFirst = !IsCaptureLeftFirst;

                var tempArray = LeftBitmap;
                LeftBitmap = RightBitmap;
                RightBitmap = tempArray;

                if (IsCameraVisible)
                {
                    CameraColumn = CameraColumn == 0 ? 1 : 0;
                }

                if (LeftBitmap != null &&
                    RightBitmap == null)
                {
                    MoveLeftTrigger = !MoveLeftTrigger;
                }

                if (LeftBitmap == null &&
                    RightBitmap != null)
                {
                    MoveRightTrigger = !MoveRightTrigger;
                }

                Settings.IsCaptureLeftFirst = IsCaptureLeftFirst;
                PersistentStorage.Save(PersistentStorage.SETTINGS_KEY, Settings);
            });

            SaveCapturesCommand = new Command(async () =>
            {
                WorkflowStage = WorkflowStage.Saving;

                var leftBitmap = LeftBitmap;
                var rightBitmap = RightBitmap;

                SKImage leftSkImage = null;
                SKImage rightSkImage = null;
                SKImage finalImage = null;
                try
                {
                    LeftBitmap = null;
                    RightBitmap = null;

                    await Task.Delay(100); // take a break to go update the screen

                    var didSave = true;

                    byte[] finalBytesToSave;

                    if (Settings.SaveSidesSeparately)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(leftBitmap.Width, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;

                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * leftBitmap.Width, -1f * leftBitmap.Height);
                            }
                            canvas.DrawBitmap(leftBitmap, 0, 0);
                            leftSkImage = tempSurface.Snapshot();

                            canvas.Clear();
                            canvas.DrawBitmap(rightBitmap, 0, 0);
                            rightSkImage = tempSurface.Snapshot();
                        }

                        using (var encoded = leftSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        // ReSharper disable once ConditionIsAlwaysTrueOrFalse - just let it go
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);

                        using (var encoded = rightSkImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    if (Settings.SaveRedundantFirstSide)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(leftBitmap.Width, leftBitmap.Height)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * leftBitmap.Width, -1f * leftBitmap.Height);
                            }
                            canvas.DrawBitmap(IsCaptureLeftFirst ? leftBitmap : rightBitmap, 0, 0);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }

                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    var finalImageWidth = DrawTool.CalculateCanvasWidth(leftBitmap, rightBitmap,
                        LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop,
                        Settings.AddBorder ? Settings.BorderThickness : 0);
                    var finalImageHeight = DrawTool.CalculateCanvasHeight(leftBitmap, rightBitmap,
                        LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                        VerticalAlignment,
                        Settings.AddBorder ? Settings.BorderThickness : 0);

                    if (Settings.SaveForCrossView)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }
                            DrawTool.DrawImagesOnCanvas(canvas, leftBitmap, rightBitmap, 
                                Settings.BorderThickness, Settings.AddBorder, Settings.BorderColor,
                                LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop,
                                LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                                LeftRotation, RightRotation, 
                                VerticalAlignment,
                                LeftZoom, RightZoom,
                                LeftKeystone, RightKeystone);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }
                        
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    if (Settings.SaveForParallel)
                    {
                        using (var tempSurface =
                            SKSurface.Create(new SKImageInfo(finalImageWidth, finalImageHeight)))
                        {
                            var canvas = tempSurface.Canvas;
                            canvas.Clear();
                            if (_needs180Flip)
                            {
                                canvas.RotateDegrees(180);
                                canvas.Translate(-1f * finalImageWidth, -1f * finalImageHeight);
                            }
                            DrawTool.DrawImagesOnCanvas(canvas, leftBitmap, rightBitmap, 
                                Settings.BorderThickness, Settings.AddBorder, Settings.BorderColor,
                                LeftLeftCrop, LeftRightCrop, RightLeftCrop, RightRightCrop,
                                LeftTopCrop, LeftBottomCrop, RightTopCrop, RightBottomCrop,
                                LeftRotation, RightRotation, VerticalAlignment,
                                LeftZoom, RightZoom,
                                LeftKeystone, RightKeystone,
                                true);

                            finalImage = tempSurface.Snapshot();
                        }

                        using (var encoded = finalImage.Encode(SKEncodedImageFormat.Jpeg, 100))
                        {
                            finalBytesToSave = encoded.ToArray();
                        }
                        
                        didSave = didSave && await photoSaver.SavePhoto(finalBytesToSave);
                    }

                    WorkflowStage = WorkflowStage.Capture;

                    if (didSave)
                    {
                        SuccessFadeTrigger = !SuccessFadeTrigger;
                    }
                    else
                    {
                        FailFadeTrigger = !FailFadeTrigger;
                    }
                }
                catch (Exception e)
                {
                    ErrorMessage = e.ToString();

                    WorkflowStage = WorkflowStage.Capture;

                    FailFadeTrigger = !FailFadeTrigger;
                }
                finally
                {
                    rightSkImage?.Dispose();
                    leftSkImage?.Dispose();
                    finalImage?.Dispose();
                    leftBitmap.Dispose();
                    rightBitmap.Dispose();

                    ClearCaptures();
                }
            });

            PromptForPermissionAndSendErrorEmailCommand = new Command(async () =>
            {
                var sendReport = await CoreMethods.DisplayAlert("Error",
                    "An error has occurred. Would you like to send an error report?", "Yes", "No");
                if (sendReport)
                {
                    var appVersionProvider = DependencyService.Get<IAppVersionProvider>();
                    var errorMessage = appVersionProvider.GetAppVersion() + 
                                       "\n" + 
                                       ErrorMessage;
                    Device.OpenUri(new Uri("mailto:me@kra2008.com?subject=CrossCam%20error%20report&body=" +
                                           HttpUtility.UrlEncode(errorMessage)));
                }

                ErrorMessage = null;
            });
        }

        private SKBitmap GetBitmapAndCorrectOrientation(byte[] byteArray)
        {
            SKCodecOrigin origin;

            using (var stream = new MemoryStream(byteArray))
            using (var data = SKData.Create(stream))
            using (var codec = SKCodec.Create(data))
            {
                origin = codec.Origin;
            }

            switch (origin)
            {
                case SKCodecOrigin.BottomRight:
                    _needs180Flip = true;
                    return BitmapRotate180(SKBitmap.Decode(byteArray));
                case SKCodecOrigin.RightTop:
                    _needs180Flip = false;
                    return BitmapRotate90(SKBitmap.Decode(byteArray));
                default:
                    _needs180Flip = false;
                    return SKBitmap.Decode(byteArray);
            }
        }

        private static SKBitmap BitmapRotate90(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Height, originalBitmap.Width);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, 0);
                surface.RotateDegrees(90);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        private static SKBitmap BitmapRotate180(SKBitmap originalBitmap)
        {
            var rotated = new SKBitmap(originalBitmap.Width, originalBitmap.Height);

            using (var surface = new SKCanvas(rotated))
            {
                surface.Translate(rotated.Width, rotated.Height);
                surface.RotateDegrees(180);
                surface.DrawBitmap(originalBitmap, 0, 0);
            }

            return rotated;
        }

        protected override void ViewIsAppearing(object sender, EventArgs e)
        {
            base.ViewIsAppearing(sender, e);
            RaisePropertyChanged(nameof(ShouldLineGuidesBeVisible)); //TODO: figure out how to have Fody do this
            RaisePropertyChanged(nameof(ShouldDonutGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldRollGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldPitchGuideBeVisible));
            RaisePropertyChanged(nameof(ShouldYawGuideBeVisible));
            RaisePropertyChanged(nameof(RightReticleImage));
            RaisePropertyChanged(nameof(Settings)); // this doesn't cause reevaluation for above stuff, but triggers redraw of canvas
        }

        private void ClearCrops()
        {
            LeftLeftCrop = 0;
            LeftRightCrop = 0;
            RightLeftCrop = 0;
            RightRightCrop = 0;
            LeftTopCrop = 0;
            LeftBottomCrop = 0;
            RightTopCrop = 0;
            RightBottomCrop = 0;
        }

        private void ClearAlignments()
        {
            LeftRotation = 0;
            RightRotation = 0;
            VerticalAlignment = 0;
            LeftZoom = 0;
            RightZoom = 0;
        }

        private void ClearKeystone()
        {
            LeftKeystone = 0;
            RightKeystone = 0;
        }

        private void ClearEdits()
        {
            ClearCrops();
            ClearAlignments();
            ClearKeystone();
        }

        private void ClearCaptures()
        {
            LeftBitmap = null;
            RightBitmap = null;
            IsCameraVisible = true;
            ClearEdits();
            WorkflowStage = WorkflowStage.Capture;

            if (Settings.IsTapToFocusEnabled)
            {
                SwitchToContinuousFocusTrigger = !SwitchToContinuousFocusTrigger;
            }
        }
    }
}