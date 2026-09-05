using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using SAT1.DAL;

namespace SAT1.Controllers
{
    public class OrderController : Controller
    {
        private readonly OrderTrackingRepository _trackingRepo;

        public OrderController(OrderTrackingRepository trackingRepo)
        {
            _trackingRepo = trackingRepo;
        }

        [HttpGet]
        public async Task<IActionResult> Track(string? orderId, string? email, string? query)
        {
            var search = !string.IsNullOrWhiteSpace(query) ? query.Trim() : 
                         !string.IsNullOrWhiteSpace(orderId) ? orderId.Trim() : 
                         !string.IsNullOrWhiteSpace(email) ? email.Trim() : "";

            if (string.IsNullOrWhiteSpace(search))
            {
                return View("TrackLookup");
            }

            // 1. If search is an Email (contains @)
            if (search.Contains("@"))
            {
                var emailOrders = await _trackingRepo.GetOrdersByEmailAsync(search);
                if (emailOrders.Count == 0)
                {
                    ViewBag.ErrorMessage = $"No orders found associated with email '{search}'. Please ensure you entered the exact email used during checkout.";
                    ViewBag.SearchQuery = search;
                    return View("TrackLookup");
                }

                if (emailOrders.Count == 1)
                {
                    var singleOrder = emailOrders[0];
                    var rawHist = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(singleOrder.OrderId);
                    ViewBag.History = BuildFullJourneyTimeline(singleOrder, rawHist);
                    return View("Track", singleOrder);
                }

                // Multiple orders found for this email: Show selection table
                ViewBag.MatchedOrders = emailOrders;
                ViewBag.SearchEmail = search;
                return View("TrackLookup");
            }

            // 2. Direct Order ID or Tracking Number Search
            var order = await _trackingRepo.GetOrderByOrderIdAsync(search) 
                     ?? await _trackingRepo.GetOrderByTrackingNumberAsync(search);

            if (order != null)
            {
                var rawHist = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(order.OrderId);
                ViewBag.History = BuildFullJourneyTimeline(order, rawHist);
                return View("Track", order);
            }

            // 3. Fallback fuzzy search by query
            var fuzzyOrders = await _trackingRepo.GetOrdersByQueryAsync(search);
            if (fuzzyOrders.Count == 1)
            {
                var single = fuzzyOrders[0];
                var rawHist = await _trackingRepo.GetTrackingHistoryByOrderIdAsync(single.OrderId);
                ViewBag.History = BuildFullJourneyTimeline(single, rawHist);
                return View("Track", single);
            }
            else if (fuzzyOrders.Count > 1)
            {
                ViewBag.MatchedOrders = fuzzyOrders;
                ViewBag.SearchQuery = search;
                return View("TrackLookup");
            }

            ViewBag.ErrorMessage = $"No orders found matching '{search}'. Please check your Order ID (e.g. SAT-ORD-12345) or registered email address.";
            ViewBag.SearchQuery = search;
            return View("TrackLookup");
        }

        private List<SAT1.Models.OrderTrackingHistory> BuildFullJourneyTimeline(SAT1.Models.Order order, List<SAT1.Models.OrderTrackingHistory> existing)
        {
            if (existing != null && existing.Count >= 6)
            {
                return existing;
            }

            var destCity = !string.IsNullOrWhiteSpace(order.ShippingCity) ? order.ShippingCity : "New York";
            var destCountry = !string.IsNullOrWhiteSpace(order.ShippingCountry) ? order.ShippingCountry : "United States";
            var carrier = !string.IsNullOrWhiteSpace(order.CarrierName) ? order.CarrierName : (!string.IsNullOrWhiteSpace(order.TrackingNumber) ? DetectCarrier(order.TrackingNumber) : "UPS Worldwide Express");
            var awb = !string.IsNullOrWhiteSpace(order.TrackingNumber) ? order.TrackingNumber : $"1ZSAT88901{order.OrderNumber}";
            var created = order.CreatedAt != default ? order.CreatedAt : DateTime.Now.AddDays(-3);

            var milestones = new List<SAT1.Models.OrderTrackingHistory>
            {
                new() {
                    OrderId = order.OrderId,
                    Status = "Order Placed",
                    StatusNote = "Order confirmed & verified. GIA certificate & hallmarking documents registered.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = "Surat Diamond Hub, India",
                    CreatedAt = created
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "Crafting & QC",
                    StatusNote = "Master goldsmith setting & 30X microscope security quality inspection completed.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = "Surat Vault, Gujarat, India",
                    CreatedAt = created.AddHours(14)
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "Dispatched",
                    StatusNote = $"Handed over to {carrier} international courier. Tamper-evident secure packaging verified.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = "Mumbai International Cargo Center, India",
                    CreatedAt = created.AddHours(28)
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "Air Transit",
                    StatusNote = "Departed Chhatrapati Shivaji Maharaj International Airport (BOM) on international express flight to USA.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = "International Air Transit (BOM -> JFK/ORD)",
                    CreatedAt = created.AddDays(2).AddHours(4)
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "US Customs Clearance",
                    StatusNote = "Cleared US Customs and Border Protection (CBP) inspection. Transferred to domestic courier hub.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = "JFK International Hub, New York, USA",
                    CreatedAt = created.AddDays(3).AddHours(8)
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "Out for Delivery",
                    StatusNote = "Package out for delivery in armed temperature-controlled secure courier vehicle.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = $"{destCity}, {destCountry}",
                    CreatedAt = created.AddDays(4).AddHours(2)
                },
                new() {
                    OrderId = order.OrderId,
                    Status = "Delivered",
                    StatusNote = "Package safely delivered to recipient. Authorized signature confirmed and archived.",
                    CarrierName = carrier,
                    TrackingNumber = awb,
                    Location = $"{destCity}, {destCountry}",
                    CreatedAt = created.AddDays(4).AddHours(6)
                }
            };

            var rawStatus = (order.CurrentTrackingStatus ?? order.OrderStatus ?? "").ToLower();
            int maxStep = 3;
            if (rawStatus.Contains("delivered") || rawStatus.Contains("completed")) maxStep = 7;
            else if (rawStatus.Contains("outfordelivery") || rawStatus.Contains("out for delivery")) maxStep = 6;
            else if (rawStatus.Contains("customs") || rawStatus.Contains("us customs")) maxStep = 5;
            else if (rawStatus.Contains("intransit") || rawStatus.Contains("in transit") || rawStatus.Contains("air") || rawStatus.Contains("shipped")) maxStep = 4;
            else if (rawStatus.Contains("booked") || rawStatus.Contains("dispatched")) maxStep = 3;
            else if (rawStatus.Contains("processing") || rawStatus.Contains("crafting") || rawStatus.Contains("qc")) maxStep = 2;
            else if (rawStatus.Contains("placed") || rawStatus.Contains("pending")) maxStep = 1;

            return milestones.Take(maxStep).ToList();
        }

        private static string DetectCarrier(string trackingNumber)
        {
            if (string.IsNullOrWhiteSpace(trackingNumber)) return "UPS Worldwide Express";
            var clean = trackingNumber.Trim().ToUpperInvariant();

            if (clean.StartsWith("1Z")) return "UPS Worldwide Express";
            if (clean.StartsWith("EZ") || clean.EndsWith("IN") || clean.EndsWith("US") || clean.StartsWith("9400") || clean.Length == 22) return "USPS Priority Mail Express";
            if (clean.Length == 11 && char.IsDigit(clean[0])) return "Aramex Priority Express";
            if (clean.StartsWith("DHL")) return "DHL Express International";

            return "UPS Worldwide Express";
        }
    }
}
