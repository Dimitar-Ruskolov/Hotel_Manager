using Hotel_Manager.Models;
using Hotel_Manager.Services;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace HotelManager.Tests.Services
{
    [TestFixture]
    public class ReservationTotalPriceServiceTests
    {
        private ReservationTotalPriceService _service = null!;

        [SetUp]
        public void Setup()
        {
            _service = new ReservationTotalPriceService();
        }

        [Test]
        public void CalculateTotalPrice_NormalValidStay_ReturnsRoomsPlusServices()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 3, 10),
                checkOut: new DateTime(2026, 3, 15),
                roomPricePerNight: 120m,
                servicePrices: new[] { 30m, 25m }
            );

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(655m));
        }

        [Test]
        public void CalculateTotalPrice_MultipleRoomsAndServices_ReturnsCorrectSum()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 4, 1),
                checkOut: new DateTime(2026, 4, 4),
                roomPricePerNight: 100m,
                servicePrices: new[] { 40m }
            );

            reservation.ReservationRooms.Add(new ReservationRoom
            {
                Room = new Room { RoomType = new RoomType { PricePerNight = 80m } }
            });

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(580m));
        }

        [Test]
        public void CalculateTotalPrice_SameDay_ReturnsServicesTotalOnly()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 5, 5),
                checkOut: new DateTime(2026, 5, 5),
                roomPricePerNight: 0m,
                servicePrices: new[] { 50m, 20m }
            );

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(70m));
        }

        [Test]
        public void CalculateTotalPrice_CheckOutBeforeCheckIn_ReturnsServicesTotalOnly()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 6, 10),
                checkOut: new DateTime(2026, 6, 5),
                roomPricePerNight: 0m,
                servicePrices: new[] { 15m }
            );

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(15m));
        }

        [Test]
        public void CalculateTotalPrice_NoServicesAndInvalidDates_ReturnsZero()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 7, 1),
                checkOut: new DateTime(2026, 7, 1),
                roomPricePerNight: 200m,
                servicePrices: Array.Empty<decimal>()
            );

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(0m));
        }

        [Test]
        public void CalculateTotalPrice_NoRoomsNoServicesValidDates_ReturnsZero()
        {
            var reservation = CreateReservation(
                checkIn: new DateTime(2026, 8, 1),
                checkOut: new DateTime(2026, 8, 5),
                roomPricePerNight: 0m,
                servicePrices: Array.Empty<decimal>()
            );
            reservation.ReservationRooms.Clear();

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(0m));
        }

        [Test]
        public void CalculateTotalPrice_NullCollectionsHandledSafely_ReturnsZero()
        {
            var reservation = new Reservation
            {
                CheckInDate = new DateTime(2026, 9, 1),
                CheckOutDate = new DateTime(2026, 9, 6),
                ReservationRooms = null,
                ReservationServices = null
            };

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(0m));
        }

        private Reservation CreateReservation(DateTime checkIn, DateTime checkOut, decimal roomPricePerNight, decimal[] servicePrices)
        {
            var reservation = new Reservation
            {
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                UserId = "test-user",
                ReservationRooms = new List<ReservationRoom>
                {
                    new() { Room = new Room { RoomType = new RoomType { PricePerNight = roomPricePerNight } } }
                },
                ReservationServices = new List<ReservationService>()
            };

            foreach (var price in servicePrices)
            {
                reservation.ReservationServices.Add(new ReservationService
                {
                    HotelService = new HotelService { Price = price }
                });
            }

            return reservation;
        }
    }
}