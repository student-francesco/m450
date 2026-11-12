using Moq;

namespace TestDoublesExercise.Tests;

// Aufgabe 1: ISeatAvailability erlaubt bereits die Aussage, dass GetAvailableSeats
// für einen Veranstaltungscode eine ganze Zahl liefert. ReservationService lässt sich
// damit testen, obwohl die produktive Implementierung fehlt, weil der Service nur
// diese Methode braucht. Fachlich nehmen wir an, dass der Rückgabewert die Anzahl der
// aktuell freien Plätze genau dieses Veranstaltungscodes ist; der Stub gibt die Zahl
// fest vor und berechnet keine echte Verfügbarkeit.
//
// Aufgabe 3: Der Stub gibt einen fest vorgegebenen Wert zurück, während der Spy die
// Aufrufe und die übergebenen Reservierungen in der Reihenfolge aufzeichnet, damit
// der Test sie anschliessend prüfen kann.
//
// Aufgabe 5: Für alle drei Abhängigkeiten wähle ich handgeschriebene Doubles. Der
// Verfügbarkeits-Stub, das aufzeichnende Repository und der Bestätigungs-Spy zeigen
// ohne weiteres Setup, dass keine Methode aufgerufen wurde. Moq lohnt sich erst, wenn
// ein Rückgabewert oder eine genaue Argumentprüfung eingerichtet werden muss; das ist
// bei dieser Vorab-Prüfung nicht der Fall.
//
// Fortgeschritten: Wirft Send nach erfolgreichem Speichern eine Exception, bleibt die
// Reservierung gespeichert, während die Bestätigung fehlt oder nur für einen Teil der
// Gruppe versendet wurde. Fachlich kann man die Bestätigung später erneut zustellen,
// ohne die Reservierung ein zweites Mal zu speichern.
public sealed class ReservationServiceTests
{
    [Fact]
    public void Reserve_WithEnoughSeats_ReturnsTrue()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 3);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var reserved = service.Reserve("ada@example.com", "CONF-2026", 2);

        // Assert
        Assert.True(reserved);
    }

    [Fact]
    public void Reserve_WithTooFewSeats_DoesNotSaveOrSend()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 1);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var reserved = service.Reserve("ada@example.com", "CONF-2026", 2);

        // Assert
        Assert.False(reserved);
        Assert.Equal("CONF-2026", Assert.Single(availability.RequestedEventCodes));
        Assert.Empty(repository.ReceivedReservations);
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void Reserve_WithExactlyEnoughSeats_Succeeds()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 2);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var reserved = service.Reserve("ada@example.com", "CONF-2026", 2);

        // Assert
        Assert.True(reserved);
    }

    [Fact]
    public void Reserve_WithZeroSeats_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Reserve("ada@example.com", "CONF-2026", 0));

        // Assert
        Assert.Equal("seatCount", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void Reserve_WithNegativeSeats_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.Reserve("ada@example.com", "CONF-2026", -1));

        // Assert
        Assert.Equal("seatCount", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void Reserve_WithEmptyEventCode_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            service.Reserve("ada@example.com", "", 2));

        // Assert
        Assert.Equal("eventCode", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void Reserve_WithWhitespaceEmail_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            service.Reserve("   ", "CONF-2026", 2));

        // Assert
        Assert.Equal("customerEmail", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void Reserve_WhenSavingFails_DoesNotSendConfirmation()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 4);
        var repository = new RecordingReservationRepository(trySaveResult: false);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var reserved = service.Reserve("ada@example.com", "CONF-2026", 2);

        // Assert
        Assert.False(reserved);
        Assert.Equal(
            new Reservation("ada@example.com", "CONF-2026", 2),
            Assert.Single(repository.ReceivedReservations));
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void Reserve_WhenSaveSucceeds_SendsOneConfirmationWithEmailEventAndSeatCount()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 4);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        var expected = new Reservation("ada@example.com", "CONF-2026", 2);

        // Act
        var reserved = service.Reserve(expected.CustomerEmail, expected.EventCode, expected.SeatCount);

        // Assert
        Assert.True(reserved);
        Assert.Equal(expected, Assert.Single(repository.ReceivedReservations));
        Assert.Equal(expected, Assert.Single(sender.SentReservations));
    }

    [Fact]
    public void Reserve_WithEnoughSeats_SavesTheExactReservationOnce()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new Mock<IReservationRepository>();
        repository
            .Setup(repo => repo.TrySave(It.IsAny<Reservation>()))
            .Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var expected = new Reservation("ada@example.com", "CONF-2026", 2);

        // Act
        var reserved = service.Reserve(expected.CustomerEmail, expected.EventCode, expected.SeatCount);

        // Assert
        Assert.True(reserved);
        repository.Verify(repo => repo.TrySave(It.Is<Reservation>(saved =>
            saved.CustomerEmail == expected.CustomerEmail
            && saved.EventCode == expected.EventCode
            && saved.SeatCount == expected.SeatCount)), Times.Once());
        Assert.Equal(expected, Assert.Single(sender.SentReservations));
    }

    [Fact]
    public void Reserve_WhenRepositoryRejectsReservation_ReturnsFalseAndDoesNotSend()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new Mock<IReservationRepository>();
        repository
            .Setup(repo => repo.TrySave(It.IsAny<Reservation>()))
            .Returns(false);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        var expected = new Reservation("ada@example.com", "CONF-2026", 2);

        // Act
        var reserved = service.Reserve(expected.CustomerEmail, expected.EventCode, expected.SeatCount);

        // Assert
        Assert.False(reserved);
        repository.Verify(repo => repo.TrySave(It.Is<Reservation>(saved =>
            saved.CustomerEmail == expected.CustomerEmail
            && saved.EventCode == expected.EventCode
            && saved.SeatCount == expected.SeatCount)), Times.Once());
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void Reserve_WithTooFewSeats_DoesNotCallRepository()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 1);
        var repository = new Mock<IReservationRepository>();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);

        // Act
        var reserved = service.Reserve("ada@example.com", "CONF-2026", 2);

        // Assert
        Assert.False(reserved);
        repository.Verify(repo => repo.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void ReserveBatch_WhenCombinedSeatsExceedCapacity_DoesNotSaveOrSend()
    {
        // Arrange: 2 und 2 passen einzeln in 3 freie Plätze, zusammen aber nicht.
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 3);
        var repository = new Mock<IReservationRepository>();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        IReadOnlyList<Reservation> requests =
        [
            new("ada@example.com", "CONF-2026", 2),
            new("bea@example.com", "CONF-2026", 2),
        ];

        // Act
        var reserved = service.ReserveBatch(requests);

        // Assert
        Assert.False(reserved);
        Assert.Equal("CONF-2026", Assert.Single(availability.RequestedEventCodes));
        repository.Verify(
            repo => repo.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()),
            Times.Never());
        repository.Verify(repo => repo.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void ReserveBatch_WithNullRequests_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentNullException>(() => service.ReserveBatch(null!));

        // Assert
        Assert.Equal("requests", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void ReserveBatch_WithEmptyRequests_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);

        // Act
        var exception = Assert.Throws<ArgumentException>(() =>
            service.ReserveBatch([]));

        // Assert
        Assert.Equal("requests", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void ReserveBatch_WithNullEntry_ThrowsBeforeCallingDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        IReadOnlyList<Reservation> requests =
        [
            new("ada@example.com", "CONF-2026", 1),
            null!,
        ];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => service.ReserveBatch(requests));

        // Assert
        Assert.Equal("requests", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReserveBatch_WithBlankEmail_ThrowsBeforeCallingDependencies(string customerEmail)
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        IReadOnlyList<Reservation> requests = [new(customerEmail, "CONF-2026", 1)];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => service.ReserveBatch(requests));

        // Assert
        Assert.Equal("CustomerEmail", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void ReserveBatch_WithBlankEventCode_ThrowsBeforeCallingDependencies(string eventCode)
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        IReadOnlyList<Reservation> requests = [new("ada@example.com", eventCode, 1)];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => service.ReserveBatch(requests));

        // Assert
        Assert.Equal("EventCode", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ReserveBatch_WithNonPositiveSeatCount_ThrowsBeforeCallingDependencies(int seatCount)
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        IReadOnlyList<Reservation> requests = [new("ada@example.com", "CONF-2026", seatCount)];

        // Act
        var exception = Assert.Throws<ArgumentOutOfRangeException>(() =>
            service.ReserveBatch(requests));

        // Assert
        Assert.Equal("SeatCount", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void ReserveBatch_WithInvalidSecondRequest_DoesNotCallDependencies()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 5);
        var repository = new RecordingReservationRepository();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository, sender);
        IReadOnlyList<Reservation> requests =
        [
            new("ada@example.com", "CONF-2026", 1),
            new("   ", "CONF-2026", 1),
        ];

        // Act
        var exception = Assert.Throws<ArgumentException>(() => service.ReserveBatch(requests));

        // Assert
        Assert.Equal("CustomerEmail", exception.ParamName);
        AssertNoCalls(availability, repository, sender);
    }

    [Fact]
    public void ReserveBatch_WithSameEventWithinCapacity_QueriesAvailabilityOnceAndSavesAll()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 4);
        var repository = new Mock<IReservationRepository>();
        repository
            .Setup(repo => repo.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()))
            .Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        Reservation first = new("ada@example.com", "CONF-2026", 2);
        Reservation second = new("bea@example.com", "CONF-2026", 2);
        IReadOnlyList<Reservation> requests = [first, second];

        // Act
        var reserved = service.ReserveBatch(requests);

        // Assert
        Assert.True(reserved);
        Assert.Equal("CONF-2026", Assert.Single(availability.RequestedEventCodes));
        repository.Verify(repo => repo.TrySaveBatch(It.Is<IReadOnlyList<Reservation>>(saved =>
            saved.Count == 2
            && saved[0].CustomerEmail == first.CustomerEmail
            && saved[0].EventCode == first.EventCode
            && saved[0].SeatCount == first.SeatCount
            && saved[1].CustomerEmail == second.CustomerEmail
            && saved[1].EventCode == second.EventCode
            && saved[1].SeatCount == second.SeatCount)), Times.Once());
        repository.Verify(repo => repo.TrySave(It.IsAny<Reservation>()), Times.Never());
        Assert.Equal([first, second], sender.SentReservations);
    }

    [Fact]
    public void ReserveBatch_WithSameEventSplitByAnotherEvent_SumsSeatsAndSavesEveryRequest()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub(new Dictionary<string, int>
        {
            ["CONF-2026"] = 5,
            ["OPER-2026"] = 2,
        });
        var repository = new Mock<IReservationRepository>();
        repository
            .Setup(repo => repo.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()))
            .Returns(true);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        Reservation first = new("ada@example.com", "CONF-2026", 2);
        Reservation middle = new("bea@example.com", "OPER-2026", 2);
        Reservation last = new("cara@example.com", "CONF-2026", 2);
        IReadOnlyList<Reservation> requests = [first, middle, last];

        // Act
        var reserved = service.ReserveBatch(requests);

        // Assert
        Assert.True(reserved);
        Assert.Equal(["CONF-2026", "OPER-2026"], availability.RequestedEventCodes);
        repository.Verify(repo => repo.TrySaveBatch(It.Is<IReadOnlyList<Reservation>>(saved =>
            saved.Count == 3
            && saved[0].CustomerEmail == first.CustomerEmail
            && saved[0].EventCode == first.EventCode
            && saved[0].SeatCount == first.SeatCount
            && saved[1].CustomerEmail == middle.CustomerEmail
            && saved[1].EventCode == middle.EventCode
            && saved[1].SeatCount == middle.SeatCount
            && saved[2].CustomerEmail == last.CustomerEmail
            && saved[2].EventCode == last.EventCode
            && saved[2].SeatCount == last.SeatCount)), Times.Once());
        Assert.Equal([first, middle, last], sender.SentReservations);
    }

    [Fact]
    public void ReserveBatch_WhenOneEventHasTooFewSeats_QueriesEachEventOnceAndDoesNotSave()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub(new Dictionary<string, int>
        {
            ["CONF-2026"] = 2,
            ["OPER-2026"] = 1,
        });
        var repository = new Mock<IReservationRepository>();
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        IReadOnlyList<Reservation> requests =
        [
            new("ada@example.com", "CONF-2026", 2),
            new("bea@example.com", "OPER-2026", 3),
        ];

        // Act
        var reserved = service.ReserveBatch(requests);

        // Assert
        Assert.False(reserved);
        Assert.Equal(["CONF-2026", "OPER-2026"], availability.RequestedEventCodes);
        repository.Verify(
            repo => repo.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()),
            Times.Never());
        Assert.Empty(sender.SentReservations);
    }

    [Fact]
    public void ReserveBatch_WhenBatchSaveFails_ReturnsFalseAndDoesNotSend()
    {
        // Arrange
        var availability = new FixedSeatAvailabilityStub("CONF-2026", 4);
        var repository = new Mock<IReservationRepository>();
        repository
            .Setup(repo => repo.TrySaveBatch(It.IsAny<IReadOnlyList<Reservation>>()))
            .Returns(false);
        var sender = new ConfirmationSenderSpy();
        var service = new ReservationService(availability, repository.Object, sender);
        Reservation first = new("ada@example.com", "CONF-2026", 1);
        Reservation second = new("bea@example.com", "CONF-2026", 1);
        IReadOnlyList<Reservation> requests = [first, second];

        // Act
        var reserved = service.ReserveBatch(requests);

        // Assert
        Assert.False(reserved);
        repository.Verify(repo => repo.TrySaveBatch(It.Is<IReadOnlyList<Reservation>>(saved =>
            saved.Count == 2
            && saved[0].CustomerEmail == first.CustomerEmail
            && saved[0].EventCode == first.EventCode
            && saved[0].SeatCount == first.SeatCount
            && saved[1].CustomerEmail == second.CustomerEmail
            && saved[1].EventCode == second.EventCode
            && saved[1].SeatCount == second.SeatCount)), Times.Once());
        Assert.Empty(sender.SentReservations);
    }

    private static void AssertNoCalls(
        FixedSeatAvailabilityStub availability,
        RecordingReservationRepository repository,
        ConfirmationSenderSpy sender)
    {
        Assert.Empty(availability.RequestedEventCodes);
        Assert.Empty(repository.ReceivedReservations);
        Assert.Empty(repository.ReceivedBatches);
        Assert.Empty(sender.SentReservations);
    }

    private sealed class FixedSeatAvailabilityStub : ISeatAvailability
    {
        private readonly Dictionary<string, int> _availableSeatsByEvent;
        private readonly List<string> _requestedEventCodes = [];

        public FixedSeatAvailabilityStub(string eventCode, int availableSeats)
            : this(new Dictionary<string, int> { [eventCode] = availableSeats })
        {
        }

        public FixedSeatAvailabilityStub(IReadOnlyDictionary<string, int> availableSeatsByEvent)
        {
            _availableSeatsByEvent = new Dictionary<string, int>(availableSeatsByEvent);
        }

        public IReadOnlyList<string> RequestedEventCodes => _requestedEventCodes;

        public int GetAvailableSeats(string eventCode)
        {
            _requestedEventCodes.Add(eventCode);
            return _availableSeatsByEvent[eventCode];
        }
    }

    private sealed class RecordingReservationRepository : IReservationRepository
    {
        private readonly bool _trySaveResult;
        private readonly List<Reservation> _receivedReservations = [];
        private readonly List<IReadOnlyList<Reservation>> _receivedBatches = [];

        public RecordingReservationRepository(bool trySaveResult = true)
        {
            _trySaveResult = trySaveResult;
        }

        public IReadOnlyList<Reservation> ReceivedReservations => _receivedReservations;

        public IReadOnlyList<IReadOnlyList<Reservation>> ReceivedBatches => _receivedBatches;

        public bool TrySave(Reservation reservation)
        {
            _receivedReservations.Add(reservation);
            return _trySaveResult;
        }

        public bool TrySaveBatch(IReadOnlyList<Reservation> reservations)
        {
            _receivedBatches.Add(reservations);
            return true;
        }
    }

    private sealed class ConfirmationSenderSpy : IConfirmationSender
    {
        private readonly List<Reservation> _sentReservations = [];

        public IReadOnlyList<Reservation> SentReservations => _sentReservations;

        public void Send(Reservation reservation) => _sentReservations.Add(reservation);
    }
}
