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
        private ReservationTotalPriceService _service;

        [SetUp]
        public void Setup()
        {
            _service = new ReservationTotalPriceService();
        }

        [Test]
        public void CalculateTotalPrice_ValidStayWithMultipleRoomsAndServices_ReturnsCorrectSum()
        {
            var reservation = new Reservation
            {
                CheckInDate = new DateTime(2026, 5, 1),
                CheckOutDate = new DateTime(2026, 5, 4),
                ReservationRooms = new List<ReservationRoom>
                {
                    new() { Room = new Room { RoomType = new RoomType { PricePerNight = 100m } } },
                    new() { Room = new Room { RoomType = new RoomType { PricePerNight = 150m } } }
                },
                ReservationServices = new List<ReservationService>
                {
                    new() { HotelService = new HotelService { Price = 40m } },
                    new() { HotelService = new HotelService { Price = 25m } }
                },
                UserId = "user-test"
            };

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(815m));
        }

        [Test]
        public void CalculateTotalPrice_NoRoomsOnlyServices_ReturnsServicesTotal()
        {
            var reservation = new Reservation
            {
                CheckInDate = new DateTime(2026, 6, 10),
                CheckOutDate = new DateTime(2026, 6, 13),
                ReservationServices = new List<ReservationService>
                {
                    new() { HotelService = new HotelService { Price = 30m } }
                },
                UserId = "user-test"
            };

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(30m));
        }

        [Test]
        public void CalculateTotalPrice_NoServicesOnlyRooms_ReturnsRoomTotal()
        {
            var reservation = CreateValidReservation(4, 90m, Array.Empty<decimal>());

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(360m));
        }

        [Test]
        public void CalculateTotalPrice_ZeroNights_ReturnsZero()
        {
            var reservation = new Reservation
            {
                CheckInDate = new DateTime(2026, 7, 1),
                CheckOutDate = new DateTime(2026, 7, 1),
                UserId = "user-test"
            };

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(0m));
        }

        [Test]
        public void CalculateTotalPrice_NegativeNights_ReturnsZero()
        {
            var reservation = new Reservation
            {
                CheckInDate = new DateTime(2026, 7, 10),
                CheckOutDate = new DateTime(2026, 7, 5),
                UserId = "user-test"
            };

            var price = _service.CalculateTotalPrice(reservation);

            Assert.That(price, Is.EqualTo(0m));
        }

        private Reservation CreateValidReservation(int nights, decimal roomPrice, decimal[] servicePrices)
        {
            var checkIn = new DateTime(2026, 3, 10);
            var checkOut = checkIn.AddDays(nights);

            var rooms = new List<ReservationRoom>
            {
                new ReservationRoom { Room = new Room { RoomType = new RoomType { PricePerNight = roomPrice } } }
            };

            var services = new List<ReservationService>();
            foreach (var price in servicePrices)
                services.Add(new ReservationService { HotelService = new HotelService { Price = price } });

            return new Reservation
            {
                CheckInDate = checkIn,
                CheckOutDate = checkOut,
                ReservationRooms = rooms,
                ReservationServices = services,
                UserId = "dummy-user-id"
            };
        }
    }
}