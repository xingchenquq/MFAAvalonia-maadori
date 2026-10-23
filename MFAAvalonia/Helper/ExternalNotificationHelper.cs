using MailKit.Net.Smtp;
using MailKit.Security;
using MFAAvalonia.Extensions;
using MFAAvalonia.ViewModels.UsersControls.Settings;
using MFAAvalonia.Helper;
using MimeKit;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MFAAvalonia.Helper;

public static class ExternalNotificationHelper
{
    #region 总调用入口

    public async static Task ExternalNotificationAsync(
        string message,
        string? eventType = null,
        bool qqBotOnly = false,
        byte[]? imageBytes = null,
        string? imageFileName = null,
        string? logText = null,
        byte[]? logImageBytes = null,
        CancellationToken cancellationToken = default)
    {
        var enabledProviders = ExternalNotificationSettingsUserControlModel.EnabledExternalNotificationProviderList;

        foreach (var enabledProvider in enabledProviders)
        {
            // qqBotOnly：像"任务开始"这类只在 QQ 机器人渠道推送的事件，跳过其它渠道
            if (qqBotOnly && enabledProvider != Key.QQBotKey)
                continue;

            switch (enabledProvider)
            {
                case Key.DingTalkKey:
                    await DingTalk.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.DingTalkToken,
                        Instances.ExternalNotificationSettingsUserControlModel.DingTalkSecret, message,
                        cancellationToken
                    );
                    break;
                case Key.EmailKey:
                    await Email.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.EmailAccount,
                        Instances.ExternalNotificationSettingsUserControlModel.EmailSecret, message,
                        cancellationToken
                    );
                    break;
                case Key.LarkKey:
                    await Lark.SendAsync(Instances.ExternalNotificationSettingsUserControlModel.LarkWebhookUrl,
                        Instances.ExternalNotificationSettingsUserControlModel.LarkId,
                        Instances.ExternalNotificationSettingsUserControlModel.LarkToken, message,
                        cancellationToken
                    );
                    break;
                case Key.WxPusherKey:
                    await WxPusher.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.WxPusherToken,
                        Instances.ExternalNotificationSettingsUserControlModel.WxPusherUid, message,
                        cancellationToken
                    );
                    break;
                case Key.TelegramKey:
                    await Telegram.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.TelegramBotToken,
                        Instances.ExternalNotificationSettingsUserControlModel.TelegramChatId, message,
                        cancellationToken
                    );
                    break;
                case Key.DiscordKey:
                    await Discord.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.DiscordChannelId,
                        Instances.ExternalNotificationSettingsUserControlModel.DiscordBotToken, message,
                        cancellationToken
                    );
                    break;
                case Key.DiscordWebhookKey:
                    await DiscordWebhook.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.DiscordWebhookName,
                        Instances.ExternalNotificationSettingsUserControlModel.DiscordWebhookUrl, message,
                        cancellationToken
                    );
                    break;
                case Key.SmtpKey:
                    await Smtp.SendAsync(Instances.ExternalNotificationSettingsUserControlModel.SmtpServer, Instances.ExternalNotificationSettingsUserControlModel.SmtpPort, Instances.ExternalNotificationSettingsUserControlModel.SmtpUseSsl,
                        Instances.ExternalNotificationSettingsUserControlModel.SmtpRequireAuthentication, Instances.ExternalNotificationSettingsUserControlModel.SmtpFrom, Instances.ExternalNotificationSettingsUserControlModel.SmtpTo,
                        Instances.ExternalNotificationSettingsUserControlModel.SmtpUser, Instances.ExternalNotificationSettingsUserControlModel.SmtpPassword, message, cancellationToken);
                    break;
                case Key.QmsgKey:
                    await QMsg.SendAsync(Instances.ExternalNotificationSettingsUserControlModel.QmsgServer,
                        Instances.ExternalNotificationSettingsUserControlModel.QmsgKey,
                        Instances.ExternalNotificationSettingsUserControlModel.QmsgUser,
                        Instances.ExternalNotificationSettingsUserControlModel.QmsgBot, message, cancellationToken);
                    break;
                case Key.ServerChanKey:
                    await ServerChan.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.ServerChanSendKey,
                        message,
                        cancellationToken
                    );
                    break;
                case Key.CustomWebhookKey:
                    await CustomWebhook.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.CustomWebhookUrl,
                        Instances.ExternalNotificationSettingsUserControlModel.CustomWebhookContentType,
                        Instances.ExternalNotificationSettingsUserControlModel.CustomWebhookPayloadTemplate,
                        message,
                        cancellationToken
                    );
                    break;
                case Key.QQBotKey:
                    if (!Instances.ExternalNotificationSettingsUserControlModel.QQBotVerified)
                    {
                        LoggerHelper.Warning("QQ机器人：渠道尚未完成验证，跳过本次推送");
                        break;
                    }
                    // QQ 机器人使用结构化文案；其它渠道沿用它自己的 message
                    var qqBotContent = string.IsNullOrEmpty(eventType)
                        ? message
                        : BuildNotificationMessage(eventType);
                    if (!string.IsNullOrWhiteSpace(logText))
                        qqBotContent += "\n\n" + logText;
                    await QQBot.SendAsync(
                        Instances.ExternalNotificationSettingsUserControlModel.QQBotAppId,
                        Instances.ExternalNotificationSettingsUserControlModel.QQBotSecret,
                        Instances.ExternalNotificationSettingsUserControlModel.QQBotOpenId,
                        qqBotContent,
                        cancellationToken
                    );
                    // 可选附带截图（富媒体）；失败只记日志，不影响已发出的文字
                    if (imageBytes is { Length: > 0 })
                    {
                        await QQBot.SendImageAsync(
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotAppId,
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotSecret,
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotOpenId,
                            imageBytes,
                            string.IsNullOrWhiteSpace(imageFileName) ? "maadori.png" : imageFileName,
                            cancellationToken
                        );
                    }
                    // 可选附带日志图片（富媒体）。与文字日志可共存：
                    // 文字便于复制/搜索，图片用于承载超出文字上限的完整日志。
                    if (logImageBytes is { Length: > 0 })
                    {
                        await QQBot.SendImageAsync(
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotAppId,
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotSecret,
                            Instances.ExternalNotificationSettingsUserControlModel.QQBotOpenId,
                            logImageBytes,
                            "maadori-log.png",
                            cancellationToken
                        );
                    }
                    break;
            }
        }
    }

    #endregion

    #region 文案构造

    /// <summary>
    /// 构造结构化通知文案：<c>[yyyy-MM-dd HH:mm] Maadori通知 · 事件类型</c>。
    /// </summary>
    /// <param name="eventType">事件类型的文案键（见 <see cref="LangKeys"/>）。</param>
    /// <param name="extra">可选附加正文（例如用户自定义文案）；为空则只发标题行。</param>
    public static string BuildNotificationMessage(string eventType, string? extra = null)
    {
        // 精确到秒：并发/重跑时便于区分同一分钟内的多条通知
        var head = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}]\n{LangKeys.ExternalNotificationSourceLabel.ToLocalization()} · {eventType.ToLocalization()}";
        return string.IsNullOrWhiteSpace(extra) ? head : head + "\n" + extra;
    }

    #endregion

    #region Keys

    public static class Key
    {
        public const string DingTalkKey = "DingTalk"; // 钉钉
        public const string EmailKey = "Email"; // 邮件
        public const string LarkKey = "Lark"; // 飞书
        public const string WxPusherKey = "WxPusher"; // 微信公众号
        public const string TelegramKey = "Telegram"; // 电报
        public const string DiscordKey = "Discord"; // Discord
        public const string DiscordWebhookKey = "DiscordWebhook"; // Discord Webhook
        public const string OneBotKey = "OneBot"; // OneBot
        public const string QmsgKey = "Qmsg"; // QMsg酱
        public const string SmtpKey = "SMTP"; // SMTP协议
        public const string ServerChanKey = "ServerChan"; // Server酱
        public const string CustomWebhookKey = "CustomWebhook"; // 通用Webhook
        public const string QQBotKey = "QQBot"; // QQ开放平台机器人

        public static readonly IReadOnlyList<string> AllKeys =
        [
            DingTalkKey,
            EmailKey,
            LarkKey,
            WxPusherKey,
            TelegramKey,
            DiscordKey,
            DiscordWebhookKey,
            SmtpKey,
            QmsgKey,
            ServerChanKey,
            CustomWebhookKey,
            QQBotKey,
        ];
    }

    #endregion

    #region 钉钉通知

    public static class DingTalk
    {
        public async static Task<bool> SendAsync(string accessToken, string secret, string info, CancellationToken cancellationToken = default)
        {
            var timestamp = GetTimestamp();
            var sign = CalculateSignature(timestamp, secret);
            var message = new
            {
                msgtype = "text",
                text = new
                {
                    content = info
                }
            };

            try
            {
                var apiUrl = string.IsNullOrWhiteSpace(secret) ? $"https://oapi.dingtalk.com/robot/send?access_token={accessToken}" : $"https://oapi.dingtalk.com/robot/send?access_token={accessToken}&timestamp={timestamp}&sign={sign}";
                using var client = VersionChecker.CreateHttpClientWithProxy();
                var content = new StringContent(JsonConvert.SerializeObject(message), Encoding.UTF8, "application/json");
                var response = await client.PostAsync(apiUrl, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("钉钉消息发送成功");
                    return true;
                }

                LoggerHelper.Error($"钉钉消息发送失败: {response.StatusCode} {await response.Content.ReadAsStringAsync(cancellationToken)}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("钉钉消息发送操作已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"钉钉消息发送错误: {ex.Message}");
                return false;
            }
        }

        private static string GetTimestamp()
        {
            return ((DateTimeOffset)DateTime.UtcNow).ToUnixTimeMilliseconds().ToString();
        }

        private static string CalculateSignature(string timestamp, string secret)
        {
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes($"{timestamp}\n{secret}"));
            return WebUtility.UrlEncode(Convert.ToBase64String(hash))
                .Replace("+", "%20")
                .Replace("/", "%2F")
                .Replace("=", "%3D");
        }
    }

    #endregion

    #region 邮箱通知

    public static class Email
    {
        public async static Task SendAsync(string email, string password, string info, CancellationToken cancellationToken = default)
        {
            try
            {
                var smtpConfig = GetSmtpConfigByEmail(email);
                using var client = new SmtpClient();
                var mail = CreateEmailMessage(email, info);

                await client.ConnectAsync(
                    smtpConfig.Host,
                    smtpConfig.Port,
                    smtpConfig.UseSSL ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.Auto,
                    cancellationToken
                );

                await client.AuthenticateAsync(email, password, cancellationToken);
                await client.SendAsync(mail, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                LoggerHelper.Info("邮件发送成功");
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("邮件发送操作已取消");
            }
            catch (AuthenticationException ex)
            {
                LoggerHelper.Error($"邮件认证失败: {ex.Message}");
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"邮件发送错误: {ex.Message}");
            }
        }

        private static MimeMessage CreateEmailMessage(string email, string info)
        {
            var mail = new MimeMessage();
            // 使用 Parse 方法支持 "显示名称" <邮箱地址> 格式
            mail.From.Add(MailboxAddress.Parse(email));
            mail.To.Add(MailboxAddress.Parse(email));
            mail.Subject = info;
            mail.Body = new TextPart(MimeKit.Text.TextFormat.Plain)
            {
                Text = info
            };
            return mail;
        }

        private static (string Host, int Port, bool UseSSL, string Notes) GetSmtpConfigByEmail(string email)
        {
            if (!email.Contains('@') || email.Split('@').Length != 2)
                throw new ArgumentException("无效的邮箱地址格式");

            var domain = email.Split('@')[1].ToLower().Trim();
            var configs = new Dictionary<string, (string Host, int Port, bool UseSSL, string Notes)>
            {
                ["qq.com"] = ("smtp.qq.com", 465, true, "需使用授权码"),
                ["163.com"] = ("smtp.163.com", 994, true, "推荐使用SSL"),
                // ... 其他邮箱配置保持不变
            };

            return configs.TryGetValue(domain, out var config)
                ? HandleSpecialCases(domain, config)
                : throw new Exception("不支持的邮箱服务");
        }

        private static (string Host, int Port, bool UseSSL, string Notes) HandleSpecialCases(
            string domain,
            (string Host, int Port, bool UseSSL, string Notes) config)
        {
            // 处理特殊域名逻辑
            if (domain.EndsWith(".edu.cn")) return (config.Host.Replace("[DOMAIN]", domain), 25, false, "教育邮箱");
            if (domain.EndsWith(".gov.cn")) return (config.Host.Replace("[DOMAIN]", domain), 25, false, "政府邮箱");
            return config;
        }
    }

    #endregion

    #region 飞书通知

    public static class Lark
    {
        public async static Task<bool> SendAsync(string webhookUrl, string appId, string appSecret, string info, CancellationToken cancellationToken = default)
        {
            var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
            var sign = GenerateSignature(timestamp, appSecret);

            var message = new
            {
                msg_type = "text",
                content = new
                {
                    text = info
                }
            };
            var jsonContent = new StringContent(
                JsonConvert.SerializeObject(message),
                Encoding.UTF8,
                "application/json"
            );
            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();

                // 分支1：如果webhookUrl不为空，直接使用该地址发送（通用WebHook）
                if (!string.IsNullOrWhiteSpace(webhookUrl))
                {
                    if (!IsValidWebHookUrl(webhookUrl))
                    {
                        LoggerHelper.Warning($"传入的WebHook URL不合法：{webhookUrl}（仅支持http/https协议的绝对URL）");
                    }
                    else
                    {
                        var webhookResponse = await client.PostAsync(
                            webhookUrl, // 直接使用传入的webhookUrl，无额外参数
                            jsonContent,
                            cancellationToken
                        );
                        return webhookResponse.IsSuccessStatusCode;
                    }
                }

                var response = await client.PostAsync(
                    $"https://open.feishu.cn/open-apis/bot/v2/hook/{appId}?timestamp={timestamp}&sign={sign}",
                    jsonContent,
                    cancellationToken
                );
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("飞书通知已取消");
                return false;
            }
        }

        private static bool IsValidWebHookUrl(string url)
        {
            // 1. 验证是否为绝对URL（包含协议、域名/IP）
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            // 2. 仅允许http或https协议（WebHook通用要求，避免非法协议风险）
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }

        private static string GenerateSignature(string timestamp, string secret)
        {
            var stringToSign = $"{timestamp}\n{secret}";
            using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
            var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(stringToSign));
            return Convert.ToBase64String(hash);
        }
    }

    #endregion

    #region 微信公众号通知

    public static class WxPusher
    {
        // 普通推送API地址
        private const string NormalApiUrl = "https://wxpusher.zjiecode.com/api/send/message";
        // 极简推送API地址
        private const string SimpleApiUrl = "https://wxpusher.zjiecode.com/api/send/message/simple-push";

        public async static Task<bool> SendAsync(string appToken, string uid, string info, CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();
                StringContent content;

                if (!string.IsNullOrWhiteSpace(appToken))
                {
                    // 普通推送：使用appToken和uid
                    var normalPayload = new
                    {
                        appToken,
                        content = info,
                        contentType = 1, // 默认文字类型
                        uids = new[]
                        {
                            uid
                        }
                    };
                    content = new StringContent(JsonConvert.SerializeObject(normalPayload), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(NormalApiUrl, content, cancellationToken);
                    return response.IsSuccessStatusCode;
                }
                else
                {
                    // 极简推送：appToken为空时，uid作为SPT处理
                    if (string.IsNullOrWhiteSpace(uid))
                    {
                        LoggerHelper.Error("极简推送模式下，SPT（uid参数）不能为空");
                        return false;
                    }

                    var simplePayload = new
                    {
                        content = info,
                        contentType = 1, // 默认文字类型
                        summary = info.Substring(0, Math.Min(info.Length, 20)), // 摘要取前20字
                        spt = uid // uid作为SPT传入
                    };
                    content = new StringContent(JsonConvert.SerializeObject(simplePayload), Encoding.UTF8, "application/json");
                    var response = await client.PostAsync(SimpleApiUrl, content, cancellationToken);

                    // 可选：解析响应确认推送结果（按接口文档调整）
                    if (response.IsSuccessStatusCode)
                    {
                        var result = JsonConvert.DeserializeObject<dynamic>(await response.Content.ReadAsStringAsync(cancellationToken));
                        return result?.success ?? true;
                    }
                    else
                    {
                        LoggerHelper.Error($"极简推送失败，状态码：{response.StatusCode}，响应：{await response.Content.ReadAsStringAsync(cancellationToken)}");
                        return false;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("微信推送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"推送异常：{ex.Message}", ex);
                return false;
            }
        }
    }

    #endregion

    #region Telegram通知

    public static class Telegram
    {
        public async static Task<bool> SendAsync(string botToken, string chatId, string info, CancellationToken cancellationToken = default)
        {
            var message = WebUtility.UrlEncode(info);
            var apiUrl = $"https://api.telegram.org/bot{botToken}/sendMessage?chat_id={chatId}&text={message}";

            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();
                var response = await client.GetAsync(apiUrl, cancellationToken);
                return response.IsSuccessStatusCode;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("电报通知已取消");
                return false;
            }
        }
    }

    #endregion

    #region SMTP通知

    public static class Smtp
    {
        public async static Task<bool> SendAsync(
            string host,
            string port,
            bool useSSL,
            bool requireLogin,
            string fromAddress,
            string toAddress,
            string username = "",
            string password = "",
            string info = "",
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = new SmtpClient();
                var secureOptions = useSSL ? SecureSocketOptions.SslOnConnect : SecureSocketOptions.StartTlsWhenAvailable;

                // 连接服务器（支持超时取消）
                await client.ConnectAsync(host, Convert.ToInt32(port), secureOptions, cancellationToken);

                // 认证逻辑（根据是否需要登录）
                if (requireLogin)
                {
                    ValidateCredentials(username, password);
                    await client.AuthenticateAsync(
                        new NetworkCredential(username, password),
                        cancellationToken
                    );
                }

                // 构建邮件消息
                var message = new MimeMessage();
                // 使用 Parse 方法支持 "显示名称" <邮箱地址> 格式
                message.From.Add(MailboxAddress.Parse(fromAddress));
                message.To.Add(MailboxAddress.Parse(toAddress));
                message.Subject = info;
                message.Body = new TextPart("plain")
                {
                    Text = info
                };

                // 发送操作（支持发送过程取消）
                await client.SendAsync(message, cancellationToken);
                await client.DisconnectAsync(true, cancellationToken);

                LoggerHelper.Info("SMTP邮件发送成功");
                return true;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("SMTP邮件发送已取消");
                return false;
            }
            catch (AuthenticationException ex)
            {
                LoggerHelper.Error($"SMTP认证失败: {ex.Message}");
                return false;
            }
            catch (SmtpProtocolException ex)
            {
                LoggerHelper.Error($"SMTP协议错误: {ex.Message}");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"SMTP未知错误: {ex.Message}");
                return false;
            }
        }

        private static void ValidateCredentials(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new ArgumentException("需要登录认证时用户名和密码不能为空");
            }
        }
    }

    #endregion

    #region Discord通知

    public static class Discord
    {
        public async static Task<bool> SendAsync(
            string channelId,
            string botToken,
            string info,
            CancellationToken cancellationToken = default)
        {
            const string apiUrl = "https://discord.com/api/v10/channels/{0}/messages";

            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bot", botToken);

                var payload = new
                {
                    content = info,
                    allowed_mentions = new
                    {
                        parse = new[]
                        {
                            "users"
                        }
                    }
                };

                var response = await client.PostAsync(
                    string.Format(apiUrl, channelId),
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"),
                    cancellationToken
                );

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("Discord消息发送成功");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LoggerHelper.Error($"Discord消息发送失败: {errorContent}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("Discord消息发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"Discord通信异常: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region DiscordWebhook通知

    public static class DiscordWebhook
    {
        public async static Task<bool> SendAsync(
            string webhookName,
            string webhookUrl,
            string info,
            CancellationToken cancellationToken = default)
        {
            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();

                var payload = new
                {
                    username = string.IsNullOrWhiteSpace(webhookName) ? "Notifier" : webhookName,
                    content = info,
                    allowed_mentions = new
                    {
                        parse = new[]
                        {
                            "users"
                        }
                    }
                };

                var response = await client.PostAsync(
                    webhookUrl,
                    new StringContent(JsonConvert.SerializeObject(payload), Encoding.UTF8, "application/json"),
                    cancellationToken
                );

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("Discord Webhook 訊息發送成功");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LoggerHelper.Error($"Discord Webhook 發送失敗: {errorContent}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("Discord Webhook 訊息發送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"Discord Webhook 發送異常: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region OneBot通知

    public static class OneBot
    {
        public async static Task<bool> SendAsync(
            string serverUrl,
            string apiKey,
            string userQq,
            string info,
            CancellationToken cancellationToken = default)
        {
            var apiEndpoint = $"{serverUrl}/send_msg";

            try
            {

                var content = new Dictionary<string, string>
                {
                    ["message"] = info,
                    ["user_id"] = userQq
                };

                var request = new HttpRequestMessage(HttpMethod.Post, apiEndpoint)
                {
                    Content = new StringContent(JsonConvert.SerializeObject(content), Encoding.UTF8, "application/json")
                };

                request.Headers.Add("Authorization", $"Bearer {apiKey}");

                using var client = VersionChecker.CreateHttpClientWithProxy();
                var response = await client.SendAsync(request, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode && responseBody.Contains("\"status\":ok"))
                {
                    LoggerHelper.Info("OneBot消息发送成功");
                    return true;
                }

                LoggerHelper.Error($"OneBot发送失败: {responseBody}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("OneBot消息发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"OneBot通信异常: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region QMsg酱通知

    public static class QMsg
    {
        public async static Task<bool> SendAsync(
            string serverUrl,
            string apiKey,
            string userQq,
            string botQq = "",
            string info = "",
            CancellationToken cancellationToken = default)
        {
            var apiEndpoint = $"{serverUrl}/send/{apiKey}";

            try
            {
                using var client = VersionChecker.CreateHttpClientWithProxy();
                var parameters = new Dictionary<string, string>
                {
                    ["msg"] = info,
                    ["qq"] = userQq
                };

                if (!string.IsNullOrEmpty(botQq))
                    parameters["bot"] = botQq;

                var content = new FormUrlEncodedContent(parameters);

                var response = await client.PostAsync(apiEndpoint, content, cancellationToken);
                var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

                if (response.IsSuccessStatusCode && responseBody.Contains("\"success\":true"))
                {
                    LoggerHelper.Info("QMsg消息发送成功");
                    return true;
                }

                LoggerHelper.Error($"QMsg发送失败: {responseBody}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("QMsg消息发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"QMsg通信异常: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region ServerChan通知

    public static class ServerChan
    {
        public async static Task<bool> SendAsync(string sendKey, string info, CancellationToken cancellationToken = default)
        {
            try
            {
                string url;

                // 判断 sendkey 是否以 "sctp" 开头并提取数字部分
                if (sendKey.StartsWith("sctp"))
                {
                    var match = System.Text.RegularExpressions.Regex.Match(sendKey, @"^sctp(\d+)t");
                    if (match.Success)
                    {
                        var num = match.Groups[1].Value;
                        url = $"https://{num}.push.ft07.com/send/{sendKey}.send";
                    }
                    else
                    {
                        LoggerHelper.Error("ServerChan: 无效的 sctp 类型 key 格式");
                        return false;
                    }
                }
                else
                {
                    url = $"https://sctapi.ftqq.com/{sendKey}.send";
                }

                var postData = $"title={WebUtility.UrlEncode("[MFA] Notification Service")}&desp={WebUtility.UrlEncode(info)}";

                using var client = VersionChecker.CreateHttpClientWithProxy();
                var content = new StringContent(postData, Encoding.UTF8, "application/x-www-form-urlencoded");
                var response = await client.PostAsync(url, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("ServerChan消息发送成功");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LoggerHelper.Error($"ServerChan消息发送失败: {errorContent}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("ServerChan消息发送操作已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"ServerChan消息发送错误: {ex.Message}");
                return false;
            }
        }
    }

    #endregion

    #region 通用Webhook通知

    public static class CustomWebhook
    {
        public async static Task<bool> SendAsync(
            string webhookUrl,
            string contentType,
            string payloadTemplate,
            string info,
            CancellationToken cancellationToken = default)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(webhookUrl))
                {
                    LoggerHelper.Error("通用Webhook URL不能为空");
                    return false;
                }

                if (!IsValidWebHookUrl(webhookUrl))
                {
                    LoggerHelper.Warning($"传入的Webhook URL不合法：{webhookUrl}（仅支持http/https协议的绝对URL）");
                    return false;
                }

                using var client = VersionChecker.CreateHttpClientWithProxy();

                // 处理payload模板，将{message}替换为实际消息
                var payload = string.IsNullOrWhiteSpace(payloadTemplate)
                    ? JsonConvert.SerializeObject(new
                    {
                        message = info
                    })
                    : payloadTemplate.Replace("{message}", info.Replace("\"", "\\\""));

                // 确定Content-Type
                var mediaType = string.IsNullOrWhiteSpace(contentType) ? "application/json" : contentType;
                var content = new StringContent(payload, Encoding.UTF8, mediaType);

                var response = await client.PostAsync(webhookUrl, content, cancellationToken);

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("通用Webhook消息发送成功");
                    return true;
                }

                var errorContent = await response.Content.ReadAsStringAsync(cancellationToken);
                LoggerHelper.Error($"通用Webhook消息发送失败: {response.StatusCode} {errorContent}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("通用Webhook消息发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"通用Webhook消息发送错误: {ex.Message}");
                return false;
            }
        }

        private static bool IsValidWebHookUrl(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }
            return uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps;
        }
    }

    #endregion

    #region QQ机器人通知

    public static class QQBot
    {
        // 固定沙箱环境：免公网 IP 白名单，且已实测可送达真实 QQ
        private const string ApiBase = "https://sandbox.api.sgroup.qq.com";
        private const string TokenUrl = "https://bots.qq.com/app/getAppAccessToken";
        private const int TimeoutSeconds = 10;
        private static readonly TimeSpan MinInterval = TimeSpan.FromSeconds(1);

        private static string _accessToken = string.Empty;
        private static DateTime _tokenExpireAt = DateTime.MinValue;
        private static DateTime _lastSentAt = DateTime.MinValue;
        // 节流锁与 token 锁刻意分开，避免 401 刷新重试时互相等待
        private static readonly SemaphoreSlim TokenLock = new(1, 1);
        private static readonly SemaphoreSlim ThrottleLock = new(1, 1);

        public static async Task<bool> SendAsync(string appId, string secret, string openId, string info, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(openId))
            {
                LoggerHelper.Error("QQ机器人凭据不完整，无法发送");
                return false;
            }

            try
            {
                await ThrottleAsync(cancellationToken);

                var payload = JsonConvert.SerializeObject(new { content = info, msg_type = 0 });
                var url = $"{ApiBase}/v2/users/{openId}/messages";

                var token = await GetAccessTokenAsync(appId, secret, false, cancellationToken);
                var response = await PostAsync(url, payload, token, cancellationToken);

                if (response.StatusCode == HttpStatusCode.Unauthorized)
                {
                    LoggerHelper.Info("QQ机器人收到 401，刷新 token 后重试一次");
                    token = await GetAccessTokenAsync(appId, secret, true, cancellationToken);
                    response = await PostAsync(url, payload, token, cancellationToken);
                }

                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("QQ机器人消息发送成功");
                    return true;
                }

                LoggerHelper.Error($"QQ机器人消息发送失败: {response.StatusCode} {response.Content}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("QQ机器人消息发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"QQ机器人通信异常: {ex.Message}");
                return false;
            }
        }

        private static async Task<HttpResponseMessage> PostAsync(string url, string payload, string token, CancellationToken cancellationToken)
        {
            using var client = VersionChecker.CreateHttpClientWithProxy();
            client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("QQBot", token);
            var content = new StringContent(payload, Encoding.UTF8, "application/json");
            return await client.PostAsync(url, content, cancellationToken);
        }

        private static async Task ThrottleAsync(CancellationToken cancellationToken)
        {
            await ThrottleLock.WaitAsync(cancellationToken);
            try
            {
                var wait = MinInterval - (DateTime.UtcNow - _lastSentAt);
                if (wait > TimeSpan.Zero)
                    await Task.Delay(wait, cancellationToken);
                _lastSentAt = DateTime.UtcNow;
            }
            finally
            {
                ThrottleLock.Release();
            }
        }

        public static async Task<string> GetAccessTokenAsync(string appId, string secret, bool force, CancellationToken cancellationToken = default)
        {
            await TokenLock.WaitAsync(cancellationToken);
            try
            {
                if (!force && !string.IsNullOrEmpty(_accessToken) && DateTime.UtcNow < _tokenExpireAt)
                    return _accessToken;

                using var client = VersionChecker.CreateHttpClientWithProxy();
                client.Timeout = TimeSpan.FromSeconds(TimeoutSeconds);
                var body = new StringContent(
                    JsonConvert.SerializeObject(new { appId = appId, clientSecret = secret }),
                    Encoding.UTF8, "application/json");
                var response = await client.PostAsync(TokenUrl, body, cancellationToken);
                var text = await response.Content.ReadAsStringAsync(cancellationToken);
                if (!response.IsSuccessStatusCode)
                    throw new Exception($"获取 access_token 失败: {response.StatusCode} {text}");

                var data = JsonConvert.DeserializeObject<Dictionary<string, string>>(text) ?? new Dictionary<string, string>();
                if (!data.TryGetValue("access_token", out var token) || string.IsNullOrWhiteSpace(token))
                    throw new Exception("获取 access_token 失败: 响应缺少 access_token");

                var expires = data.TryGetValue("expires_in", out var exp) && int.TryParse(exp, out var seconds) ? seconds : 7200;
                _accessToken = token;
                _tokenExpireAt = DateTime.UtcNow.AddSeconds(Math.Max(expires - 60, 30));
                return token;
            }
            finally
            {
                TokenLock.Release();
            }
        }

        // 校验凭据是否有效：能换到 access_token 即视为有效（平台没有更轻量的探测接口）
        public static async Task<bool> ValidateAsync(string appId, string secret, CancellationToken cancellationToken = default)
        {
            try
            {
                await GetAccessTokenAsync(appId, secret, true, cancellationToken);
                return true;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"QQ机器人凭据校验失败: {ex.Message}");
                return false;
            }
        }

        // ---- 富媒体（图片）----

        /// <summary>
        /// 上传 PNG 并以富媒体消息（msg_type=7）发送。
        /// 失败只记日志并返回 false，不影响已经发出的文字通知。
        /// </summary>
        public static async Task<bool> SendImageAsync(string appId, string secret, string openId, byte[] imageBytes, string fileName, CancellationToken cancellationToken = default)
        {
            if (imageBytes == null || imageBytes.Length == 0)
            {
                LoggerHelper.Warning("QQ机器人图片发送跳过：图片数据为空");
                return false;
            }
            if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(openId))
            {
                LoggerHelper.Error("QQ机器人凭据不完整，无法发送图片");
                return false;
            }

            try
            {
                await ThrottleAsync(cancellationToken);
                var token = await GetAccessTokenAsync(appId, secret, false, cancellationToken);
                var fileInfo = await UploadImageAsync(token, openId, imageBytes, fileName, cancellationToken);
                if (string.IsNullOrWhiteSpace(fileInfo))
                {
                    LoggerHelper.Warning("QQ机器人图片上传失败：未获取到 file_info，放弃发送");
                    return false;
                }

                var payload = JsonConvert.SerializeObject(new
                {
                    msg_type = 7,
                    media = new { file_info = fileInfo },
                });
                var response = await PostAsync($"{ApiBase}/v2/users/{openId}/messages", payload, token, cancellationToken);
                if (response.IsSuccessStatusCode)
                {
                    LoggerHelper.Info("QQ机器人图片发送成功");
                    return true;
                }

                LoggerHelper.Error($"QQ机器人图片发送失败: {response.StatusCode} {response.Content}");
                return false;
            }
            catch (OperationCanceledException)
            {
                LoggerHelper.Warning("QQ机器人图片发送已取消");
                return false;
            }
            catch (Exception ex)
            {
                LoggerHelper.Error($"QQ机器人图片发送异常: {ex.Message}");
                return false;
            }
        }

        // 官方未提供 base64 直传（只有 url / 分片上传两条路），本地截图必须走分片。
        // 截图一般 < 5MB（= 默认 block_size），通常只有一个分片。
    private static async Task<string?> UploadImageAsync(string token, string openId, byte[] data, string fileName, CancellationToken cancellationToken)
    {
        // 整体超时保护：沙箱/代理异常时，某一环（典型是 upload_prepare）可能长时间挂起，
        // 导致图片发送永久卡死且没有任何日志，表现为「文字能收、图片静默消失」。
        // 这里用 25s 硬超时确保快速失败，异常可被上层 SendImageAsync 捕获并记录。
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(25));
        var ct = cts.Token;

        const int Md5HeadSize = 10002432;

        var md5 = Convert.ToHexString(MD5.HashData(data)).ToLowerInvariant();
        var sha1 = Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();
        var headLength = Math.Min(data.Length, Md5HeadSize);
        var md5Head = Convert.ToHexString(MD5.HashData(data.AsSpan(0, headLength))).ToLowerInvariant();

        var prepareBody = JsonConvert.SerializeObject(new
        {
            file_type = 1,
            file_size = data.Length.ToString(),
            file_name = fileName,
            md5,
            sha1,
            md5_10m = md5Head,
        });
        LoggerHelper.Info($"QQ机器人图片上传开始：{data.Length} 字节，文件={fileName}");
        var prepareResp = await PostAsync($"{ApiBase}/v2/users/{openId}/upload_prepare", prepareBody, token, ct);
        var prepareText = await prepareResp.Content.ReadAsStringAsync(ct);
        if (!prepareResp.IsSuccessStatusCode)
        {
            LoggerHelper.Error($"QQ机器人预上传失败: {prepareResp.StatusCode} {prepareText}");
            return null;
        }

        var prepare = JObject.Parse(prepareText);
        var uploadId = prepare["upload_id"]?.ToString();
        var parts = prepare["parts"] as JArray;
        if (string.IsNullOrWhiteSpace(uploadId) || parts == null || parts.Count == 0)
        {
            LoggerHelper.Error($"QQ机器人预上传响应异常: {prepareText}");
            return null;
        }
        LoggerHelper.Info($"QQ机器人图片预上传成功：upload_id={uploadId}，分片数={parts.Count}");

        // 服务端返回的 parts[].index 从 1 开始（官方文档写的是从 0 开始，实测为 1），
        // 所以偏移必须按分片顺序累积，绝不能用 index * block_size —— 否则首片偏移就越界，
        // 上传的会是空数据，合并时返回 850019「富媒体文件格式不支持」。
        var offset = 0L;
        foreach (var part in parts)
        {
            var index = part["index"]?.Value<int>() ?? 0;
            var presignedUrl = part["presigned_url"]?.ToString();
            var blockSize = long.TryParse(part["block_size"]?.ToString(), out var bs) && bs > 0 ? bs : data.Length;
            if (string.IsNullOrWhiteSpace(presignedUrl))
                continue;

            var length = (int)Math.Min(blockSize, Math.Max(data.Length - offset, 0));
            var chunk = new byte[length];
            Array.Copy(data, offset, chunk, 0, length);
            offset += length;

            using (var http = VersionChecker.CreateHttpClientWithProxy())
            {
                http.Timeout = TimeSpan.FromSeconds(60);
                var putResp = await http.PutAsync(presignedUrl, new ByteArrayContent(chunk), ct);
                if (!putResp.IsSuccessStatusCode)
                {
                    LoggerHelper.Error($"QQ机器人分片上传失败 part={index}: {putResp.StatusCode}");
                    return null;
                }
            }

            var partMd5 = Convert.ToHexString(MD5.HashData(chunk)).ToLowerInvariant();
            var finishBody = JsonConvert.SerializeObject(new
            {
                upload_id = uploadId,
                part_index = index,
                block_size = blockSize.ToString(),
                md5 = partMd5,
            });
            var finishResp = await PostAsync($"{ApiBase}/v2/users/{openId}/upload_part_finish", finishBody, token, ct);
            if (!finishResp.IsSuccessStatusCode)
            {
                var finishText = await finishResp.Content.ReadAsStringAsync(ct);
                LoggerHelper.Error($"QQ机器人分片确认失败 part={index}: {finishResp.StatusCode} {finishText}");
                return null;
            }
        }
        LoggerHelper.Info("QQ机器人图片分片上传完成，开始合并");

        var mergeBody = JsonConvert.SerializeObject(new
        {
            upload_id = uploadId,
            file_type = 1,
            srv_send_msg = false,
        });
        var mergeResp = await PostAsync($"{ApiBase}/v2/users/{openId}/files", mergeBody, token, ct);
        var mergeText = await mergeResp.Content.ReadAsStringAsync(ct);
        if (!mergeResp.IsSuccessStatusCode)
        {
            LoggerHelper.Error($"QQ机器人上传合并失败: {mergeResp.StatusCode} {mergeText}");
            return null;
        }
        LoggerHelper.Info("QQ机器人图片合并成功");

        return JObject.Parse(mergeText)["file_info"]?.ToString();
    }
    }

    #endregion
}
