using System.Diagnostics;
using System.Windows.Input;
using CrossCam.CustomElement;
using CrossCam.Model;
using CrossCam.Wrappers;
using Moq;

namespace CrossCam.UnitTests
{
    public class PairOperatorTests
    {
        private PairOperator _primaryPairOperator;
        private Mock<IDependencyService> _fakePrimaryDependencyService;
        private Mock<IPlatformPair> _fakePrimaryPhone;
        private Settings _primarySettings;
        private Mock<INowProvider> _fakePrimaryNowProvider;
        private Mock<IDevice> _fakePrimaryDevice;

        private PairOperator _secondaryPairOperator;
        private Mock<IDependencyService> _fakeSecondaryDependencyService;
        private Mock<IPlatformPair> _fakeSecondaryPhone;
        private Settings _secondarySettings;
        private Mock<INowProvider> _fakeSecondaryNowProvider;
        private Mock<IDevice> _secondaryPrimaryDevice;

        private int _previewFrameCounter;
        private const int PREVIEW_FRAME_COUNT = 5;

        [SetUp]
        public void Setup()
        {
            _fakePrimaryPhone = new Mock<IPlatformPair>(MockBehavior.Strict);
            _fakePrimaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b =>
                    b.Contains((byte) PairOperator.CrossCommand.RequestPreviewFrame))))
                .Callback<byte[]>(payload =>
            {
                _fakeSecondaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                _secondaryPairOperator.SendLatestPreviewFrame(new byte[]{0,1,2,3});
            });
            _fakePrimaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b =>
                    b.Contains((byte) PairOperator.CrossCommand.RequestImageCapture))))
                .Callback<byte[]>(payload =>
            {
                _fakeSecondaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
            });


            _fakeSecondaryPhone = new Mock<IPlatformPair>(MockBehavior.Strict);
            _fakeSecondaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b => 
                    b.Contains((byte) PairOperator.CrossCommand.Hello))))
                .Callback<byte[]>(payload =>
                {
                    _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });
            _fakeSecondaryPhone.Setup(p => p.SendPayload(It.Is<byte[]>(b =>
                b.Contains((byte) PairOperator.CrossCommand.PreviewFrame))))
                .Callback<byte[]>(payload =>
            {
                _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                if (++_previewFrameCounter < PREVIEW_FRAME_COUNT)
                {
                    _primaryPairOperator.RequestPreviewFrame();
                }
                else if (_previewFrameCounter == PREVIEW_FRAME_COUNT)
                {
                    _primaryPairOperator.BeginSyncedCapture();
                    _primaryPairOperator.RequestPreviewFrame();
                    _previewFrameCounter++;
                }
            });
            

            _fakePrimaryDependencyService = new Mock<IDependencyService>();
            _fakePrimaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakePrimaryPhone.Object);
            _fakeSecondaryDependencyService = new Mock<IDependencyService>();
            _fakeSecondaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakeSecondaryPhone.Object);


            _fakePrimaryDevice = new Mock<IDevice>(MockBehavior.Strict);
            _secondaryPrimaryDevice = new Mock<IDevice>(MockBehavior.Strict);

            _fakePrimaryNowProvider = new Mock<INowProvider>();
            _fakeSecondaryNowProvider = new Mock<INowProvider>();

            _primarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = true,
                    TimeoutSeconds = 0,
                    PairedPreviewFrameDelayMs = 0
                }
            };
            _primaryPairOperator = new PairOperator(
                _primarySettings, _fakePrimaryDependencyService.Object, _fakePrimaryNowProvider.Object, _fakePrimaryDevice.Object);
            _secondarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = false,
                    TimeoutSeconds = 0,
                    PairedPreviewFrameDelayMs = 0
                }
            };
            _secondaryPairOperator = new PairOperator(
                _secondarySettings, _fakeSecondaryDependencyService.Object, _fakeSecondaryNowProvider.Object, _secondaryPrimaryDevice.Object);
        }

        private void SetupTimeModifier(int timeModifier)
        {
            var primaryNow = new DateTime(2000, 1, 1, 0, 0, 1);
            var primaryTimeCalls = 1;
            _fakePrimaryNowProvider.Setup(x => x.UtcNow()).Returns(() =>
            {
                primaryTimeCalls++;
                return primaryNow.AddTicks(primaryTimeCalls * timeModifier);
            });
            var secondaryNow = new DateTime(2000, 1, 1, 0, 0, 2);
            var secondaryTimeCalls = 1;
            _fakeSecondaryNowProvider.Setup(x => x.UtcNow()).Returns(() =>
            {
                secondaryTimeCalls++;
                return secondaryNow.AddTicks(secondaryTimeCalls * timeModifier);
            });
        }

        [Test]
        public async Task ShouldSomething()
        {
            const int timeModifier = 50000;
            const uint delay = 0;


            _fakePrimaryPhone.Setup(p =>
                    p.SendPayload(It.Is<byte[]>(b =>
                        b.Contains((byte)PairOperator.CrossCommand.RequestClockReading))))
                .Callback<byte[]>(payload =>
                {
                    _fakeSecondaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });

            _fakeSecondaryPhone.Setup(p =>
                    p.SendPayload(It.Is<byte[]>(b =>
                        b.Contains((byte)PairOperator.CrossCommand.ClockReading))))
                .Callback<byte[]>(payload =>
                {
                    _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });


            _primarySettings.PairSettings.PairedCaptureCountdown = delay;
            SetupTimeModifier(timeModifier);

            long? primaryCaptured = null, secondaryCaptured = null;
            _primaryPairOperator.InitialSyncCompleted += (sender, args) =>
            {
                _primaryPairOperator.RequestPreviewFrame();
            };

            var awaitingCapture = true;

            _primaryPairOperator.CaptureSyncTimeElapsed += (sender, args) =>
            {
                primaryCaptured = DateTime.UtcNow.Ticks;
                PrintTestResultIfReady(ref awaitingCapture, primaryCaptured, secondaryCaptured);
            };
            _secondaryPairOperator.CaptureSyncTimeElapsed += (sender, args) =>
            {
                secondaryCaptured = DateTime.UtcNow.Ticks;
                PrintTestResultIfReady(ref awaitingCapture, primaryCaptured, secondaryCaptured);
            };

            _fakePrimaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);
            _fakeSecondaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);

            while (awaitingCapture)
            {
                await Task.Delay(1000);
            }
        }

        private static void PrintTestResultIfReady(ref bool awaitingCapture, long? primaryCaptured, long? secondaryCaptured)
        {
            if (primaryCaptured.HasValue &&
                secondaryCaptured.HasValue)
            {
                var ms = (primaryCaptured.Value - secondaryCaptured.Value) / 10000d;
                Console.WriteLine("Capture diff: " + ms + " ms");
                awaitingCapture = false;
                Assert.Pass();
            }
        }

        public static IEnumerable<int> PatternAt(IEnumerable<byte> source, IEnumerable<byte> pattern)
        {
            for (var i = 0; i < source.Count(); i++)
            {
                if (source.Skip(i).Take(pattern.Count()).SequenceEqual(pattern))
                {
                    yield return i;
                }
            }
        }
    }
}