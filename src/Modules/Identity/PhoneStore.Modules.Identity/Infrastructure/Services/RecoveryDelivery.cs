using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using PhoneStore.BuildingBlocks.Exceptions;

namespace PhoneStore.Modules.Identity.Infrastructure.Services;

public sealed class RecoveryOptions
{
    public string HashKey { get; set; } = "";
    public SmtpRecoveryOptions Smtp { get; set; } = new();
    public SmsRecoveryOptions Sms { get; set; } = new();
}
public sealed class SmtpRecoveryOptions
{
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public string From { get; set; } = "";
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
}
public sealed class SmsRecoveryOptions
{
    public string AccountSid { get; set; } = "";
    public string AuthToken { get; set; } = "";
    public string From { get; set; } = "";
}
public interface IRecoveryDelivery
{
    void EnsureConfigured(string channel);
    Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken);
}

public sealed class RecoveryDelivery(IOptions<RecoveryOptions> options, IHttpClientFactory clients) : IRecoveryDelivery
{
    public void EnsureConfigured(string channel)
    {
        var o = options.Value;
        var configured = channel switch
        {
            "email" => !string.IsNullOrWhiteSpace(o.Smtp.Host) && o.Smtp.Port is > 0 and <= 65535
                && MailAddress.TryCreate(o.Smtp.From, out _) && !string.IsNullOrEmpty(o.Smtp.Username) && !string.IsNullOrEmpty(o.Smtp.Password),
            "sms" => Regex.IsMatch(o.Sms.AccountSid, "^AC[0-9a-fA-F]{32}$") && !string.IsNullOrWhiteSpace(o.Sms.AuthToken)
                && Regex.IsMatch(o.Sms.From, @"^\+[1-9][0-9]{7,14}$"),
            _ => false
        };
        if (!configured) throw Unavailable("RECOVERY_NOT_CONFIGURED");
    }

    public async Task SendAsync(string channel, string destination, string code, CancellationToken cancellationToken)
    {
        EnsureConfigured(channel);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            if (channel == "email")
            {
                var s = options.Value.Smtp;
                using var smtp = new SmtpClient(s.Host, s.Port)
                {
                    EnableSsl = true, UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(s.Username, s.Password)
                };
                using var message = new MailMessage(s.From, destination, "PhoneStore: mã xác minh",
                    $"Mã xác minh PhoneStore: {code}. Hết hạn sau 5 phút. Không chia sẻ mã với bất kỳ ai.");
                await smtp.SendMailAsync(message, timeout.Token);
            }
            else
            {
                var s = options.Value.Sms;
                using var request = new HttpRequestMessage(HttpMethod.Post,
                    $"https://api.twilio.com/2010-04-01/Accounts/{s.AccountSid}/Messages.json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic",
                    Convert.ToBase64String(Encoding.ASCII.GetBytes($"{s.AccountSid}:{s.AuthToken}")));
                request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["From"] = s.From, ["To"] = destination,
                    ["Body"] = $"PhoneStore code: {code}. Expires in 5 minutes. Do not share this code."
                });
                using var response = await clients.CreateClient("identity-sms").SendAsync(request, timeout.Token);
                if (response.StatusCode != HttpStatusCode.Created) throw Unavailable("RECOVERY_DELIVERY_FAILED");
                var body = await response.Content.ReadFromJsonAsync<JsonElement>(timeout.Token);
                var status = body.GetProperty("status").GetString();
                if (status is not ("queued" or "accepted" or "sending" or "sent" or "delivered"))
                    throw Unavailable("RECOVERY_DELIVERY_FAILED");
            }
        }
        catch (Exception ex) when (ex is SmtpException or HttpRequestException or JsonException or InvalidOperationException
            or KeyNotFoundException or OperationCanceledException)
        {
            // Do not attach provider exception/body: it can contain credentials, recipient, or code.
            throw Unavailable("RECOVERY_DELIVERY_FAILED");
        }
    }

    private static AppException Unavailable(string code) => new(
        "Dịch vụ gửi mã chưa sẵn sàng. Vui lòng thử lại sau hoặc liên hệ hỗ trợ.", code, HttpStatusCode.ServiceUnavailable);
}
