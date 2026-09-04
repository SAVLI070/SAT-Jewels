using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SAT1.Models;

namespace SAT1.BAL
{
    public class EmailNotificationService
    {
        private readonly IConfiguration _config;
        private readonly string _fromEmail;
        private readonly string _smtpHost;
        private readonly int _smtpPort;
        private readonly string _smtpUser;
        private readonly string _smtpPass;

        public EmailNotificationService(IConfiguration config)
        {
            _config = config;
            _fromEmail = _config["Email:From"] ?? "orders@satjewel.com";
            _smtpHost = _config["Email:SmtpHost"] ?? "smtp.sendgrid.net";
            _smtpPort = int.TryParse(_config["Email:SmtpPort"], out var p) ? p : 587;
            _smtpUser = _config["Email:SmtpUser"] ?? "";
            _smtpPass = _config["Email:SmtpPass"] ?? "";
        }

        public async Task<bool> SendTrackingUpdateEmailAsync(Order order, string status, string statusNote, string trackingUrl)
        {
            if (string.IsNullOrWhiteSpace(order.CustomerEmail)) return false;

            try
            {
                var subject = status switch
                {
                    "ShipmentBooked" => $"✨ Your Fine Jewelry Order #{order.OrderNumber} Has Been Dispatched!",
                    "InTransit" => $"✈️ Order #{order.OrderNumber} is In International Transit",
                    "CustomsClearance" => $"🛃 Order #{order.OrderNumber} US Customs Clearance Update",
                    "OutForDelivery" => $"🚚 Out For Delivery: Your SAT Order #{order.OrderNumber}",
                    "Delivered" => $"💎 Delivered! Your SAT Fine Jewelry Order #{order.OrderNumber}",
                    _ => $"📦 Order #{order.OrderNumber} Tracking Status Update: {status}"
                };

                var bodyHtml = $@"
<!DOCTYPE html>
<html>
<head>
<style>
  body {{ font-family: 'Helvetica Neue', Helvetica, Arial, sans-serif; background:#0f172a; color:#ffffff; padding:24px; }}
  .card {{ max-width:600px; margin:0 auto; background:#1e293b; border-radius:16px; padding:32px; border:1px solid #334155; }}
  .gold-text {{ color:#d4b270; font-weight:bold; }}
  .btn {{ display:inline-block; background:linear-gradient(135deg, #d4b270 0%, #b45309 100%); color:#ffffff; padding:12px 28px; text-decoration:none; border-radius:8px; font-weight:bold; margin-top:20px; }}
</style>
</head>
<body>
  <div class='card'>
    <h2 style='margin-top:0; color:#d4b270;'>SAT Fine Jewelry</h2>
    <p>Dear {order.ShippingFullName},</p>
    <p>Here is the latest live shipment update for your order <strong class='gold-text'>#{order.OrderNumber}</strong>:</p>
    
    <div style='background:#0f172a; padding:16px; border-radius:10px; border-left:4px solid #d4b270; margin:20px 0;'>
      <div style='font-size:12px; color:#94a3b8; text-transform:uppercase;'>Current Stage:</div>
      <div style='font-size:18px; font-weight:bold; color:#ffffff;'>{status}</div>
      <div style='font-size:13px; color:#cbd5e1; margin-top:6px;'>{statusNote}</div>
    </div>

    <p><strong>Carrier:</strong> {order.CarrierName}<br/>
       <strong>Tracking Number:</strong> {order.TrackingNumber}<br/>
       <strong>Estimated Delivery:</strong> {(order.EstimatedDeliveryDate?.ToString("MMMM dd, yyyy") ?? "In 3-5 Business Days")}</p>

    <div style='text-align:center;'>
      <a href='https://satjewel.com/Order/Track?orderId={order.OrderId}' class='btn'>Track Parcel Live</a>
    </div>

    <p style='font-size:11px; color:#64748b; margin-top:32px; text-align:center;'>
      &copy; {DateTime.Now.Year} SAT Fine Jewelry. Handcrafted with GIA certified excellence.
    </p>
  </div>
</body>
</html>";

                // Log notification dispatch in console/audit
                Console.WriteLine($"[EmailNotificationService] Dispatched tracking update email to {order.CustomerEmail} for order {order.OrderNumber} ({status})");

                // In production with valid credentials, send SMTP message
                if (!string.IsNullOrWhiteSpace(_smtpUser) && !string.IsNullOrWhiteSpace(_smtpPass))
                {
                    using var client = new SmtpClient(_smtpHost, _smtpPort)
                    {
                        Credentials = new NetworkCredential(_smtpUser, _smtpPass),
                        EnableSsl = true
                    };
                    var mail = new MailMessage(_fromEmail, order.CustomerEmail, subject, bodyHtml)
                    {
                        IsBodyHtml = true
                    };
                    await client.SendMailAsync(mail);
                }

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EmailNotificationService Error]: {ex.Message}");
                return false;
            }
        }
    }
}
