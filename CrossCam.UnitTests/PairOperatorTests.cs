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
        private DateTime _primaryNow = new(2000,1,1);
        private Mock<IDevice> _fakePrimaryDevice;

        private PairOperator _secondaryPairOperator;
        private Mock<IDependencyService> _fakeSecondaryDependencyService;
        private Mock<IPlatformPair> _fakeSecondaryPhone;
        private Settings _secondarySettings;
        private Mock<INowProvider> _fakeSecondaryNowProvider;
        private DateTime _secondaryNow = new(2000, 1, 1);
        private Mock<IDevice> _secondaryPrimaryDevice;

        [SetUp]
        public void Setup()
        {
            _fakePrimaryPhone = new Mock<IPlatformPair>(MockBehavior.Strict);
            _fakePrimaryPhone.Setup(p =>
                    p.SendPayload(It.Is<byte[]>(b =>
                        b.Contains((byte) PairOperator.CrossCommand.RequestClockReading))))
                .Callback<byte[]>(payload =>
                {
                    _fakeSecondaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });
            _fakePrimaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b =>
                    b.Contains((byte) PairOperator.CrossCommand.RequestPreviewFrame)))).Callback<byte[]>(payload =>
            {
                _fakeSecondaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                _secondaryPairOperator.SendLatestPreviewFrame(new byte[]{});
            });

            _fakeSecondaryPhone = new Mock<IPlatformPair>(MockBehavior.Strict);
            _fakeSecondaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b => 
                    b.Contains((byte) PairOperator.CrossCommand.Hello))))
                .Callback<byte[]>(payload =>
                {
                    _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });
            _fakeSecondaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b =>
                    b.Contains((byte) PairOperator.CrossCommand.ClockReading) &&
                    PatternAt(b.AsEnumerable(), BitConverter.GetBytes(_secondaryNow.Ticks)).Any()))).Callback<byte[]>(
                payload =>
                {
                    _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });
            _fakeSecondaryPhone.Setup(p => p.SendPayload(It.Is<byte[]>(b =>
                b.Contains((byte) PairOperator.CrossCommand.PreviewFrame)))).Callback<byte[]>(payload =>
            {
                _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                _primaryPairOperator.RequestPreviewFrame();
            });
            

            _fakePrimaryDependencyService = new Mock<IDependencyService>();
            _fakePrimaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakePrimaryPhone.Object);
            _fakeSecondaryDependencyService = new Mock<IDependencyService>();
            _fakeSecondaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakeSecondaryPhone.Object);


            _fakePrimaryNowProvider = new Mock<INowProvider>();
            _fakePrimaryNowProvider.Setup(x => x.UtcNow()).Returns(_primaryNow);
            _fakeSecondaryNowProvider = new Mock<INowProvider>();
            _fakeSecondaryNowProvider.Setup(x => x.UtcNow()).Returns(_secondaryNow);


            _fakePrimaryDevice = new Mock<IDevice>(MockBehavior.Strict);
            _secondaryPrimaryDevice = new Mock<IDevice>(MockBehavior.Strict);


            _primarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = true
                }
            };
            _primaryPairOperator = new PairOperator(
                _primarySettings, _fakePrimaryDependencyService.Object, _fakePrimaryNowProvider.Object, _fakePrimaryDevice.Object);
            _secondarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = false
                }
            };
            _secondaryPairOperator = new PairOperator(
                _secondarySettings, _fakeSecondaryDependencyService.Object, _fakeSecondaryNowProvider.Object, _secondaryPrimaryDevice.Object);
        }

        [Test]
        public void ShouldSomething()
        {
            _primaryPairOperator.InitialSyncCompleted += (sender, args) =>
            {
                _primaryPairOperator.RequestPreviewFrame();
            };

            _fakePrimaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);
            _fakeSecondaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);
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