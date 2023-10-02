using System.Diagnostics;
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

        private PairOperator _secondaryPairOperator;
        private Mock<IDependencyService> _fakeSecondaryDependencyService;
        private Mock<IPlatformPair> _fakeSecondaryPhone;
        private Settings _secondarySettings;
        private Mock<INowProvider> _fakeSecondaryNowProvider;


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

            _fakeSecondaryPhone = new Mock<IPlatformPair>(MockBehavior.Strict);
            _fakeSecondaryPhone.Setup(p =>
                p.SendPayload(It.Is<byte[]>(b => 
                    b.Contains((byte) PairOperator.CrossCommand.Hello))))
                .Callback<byte[]>(payload =>
                {
                    _fakePrimaryPhone.Raise(e => e.PayloadReceived += null, null, payload);
                });

            _fakePrimaryDependencyService = new Mock<IDependencyService>();
            _fakePrimaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakePrimaryPhone.Object);

            _fakeSecondaryDependencyService = new Mock<IDependencyService>();
            _fakeSecondaryDependencyService.Setup(x => x.Get<IPlatformPair>())
                .Returns(_fakeSecondaryPhone.Object);

            _fakePrimaryNowProvider = new Mock<INowProvider>();
            _fakePrimaryNowProvider.Setup(x => x.UtcNow()).Returns(new DateTime());
            _fakeSecondaryNowProvider = new Mock<INowProvider>();
            _fakeSecondaryNowProvider.Setup(x => x.UtcNow()).Returns(new DateTime());

            _primarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = true
                }
            };
            _primaryPairOperator = new PairOperator(
                _fakePrimaryDependencyService.Object, _fakePrimaryNowProvider.Object, _primarySettings);
            
            _secondarySettings = new Settings
            {
                PairSettings =
                {
                    IsPairedPrimary = false
                }
            };
            _secondaryPairOperator = new PairOperator(
                _fakeSecondaryDependencyService.Object, _fakeSecondaryNowProvider.Object, _secondarySettings);

        }

        [Test]
        public void ShouldSomething()
        {
            _fakePrimaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);
            _fakeSecondaryPhone.Raise(e => e.Connected += null, EventArgs.Empty);
        }
    }
}