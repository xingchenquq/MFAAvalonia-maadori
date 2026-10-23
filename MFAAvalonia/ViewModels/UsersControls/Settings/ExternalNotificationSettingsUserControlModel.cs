using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MFAAvalonia.Configuration;
using MFAAvalonia.Extensions;
using MFAAvalonia.Helper;
using MFAAvalonia.ViewModels.Other;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web;

namespace MFAAvalonia.ViewModels.UsersControls.Settings;

public partial class ExternalNotificationSettingsUserControlModel : ViewModelBase
{
    #region 初始

    protected override void Initialize()
    {
        UpdateExternalNotificationProvider();
        UpdateQQBotStatus();
    }

    public static readonly List<LocalizationViewModel> ExternalNotificationProviders = ExternalNotificationHelper.Key.AllKeys.Select(k => new LocalizationViewModel(k)).ToList();

    /// <summary>
    /// 「QQ 机器人」卡片是否可见。Maaon / MaaEOV 等非 Maadori 品牌不展示该渠道，
    /// 避免用户面对一个对本产品毫无意义的设置项。
    /// </summary>
    public bool QQBotCardVisible => BrandCapabilities.QqBotNotify;

    public void UpdateExternalNotificationProvider()
    {
        DingTalkEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.DingTalkKey);
        EmailEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.EmailKey);
        LarkEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.LarkKey);
        QmsgEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.QmsgKey);
        WxPusherEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.WxPusherKey);
        SmtpEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.SmtpKey);
        TelegramEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.TelegramKey);
        DiscordEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.DiscordKey);
        DiscordWebhookEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.DiscordWebhookKey);
        OnebotEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.OneBotKey);
        ServerChanEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.ServerChanKey);
        CustomWebhookEnabled = EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.CustomWebhookKey);
        QQBotEnabled = BrandCapabilities.QqBotNotify && EnabledExternalNotificationProviderList.Contains(ExternalNotificationHelper.Key.QQBotKey);
    }

    public static readonly List<string> EnabledExternalNotificationProviderList = ExternalNotificationProviders
        .Where(s => ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationEnabled, string.Empty).Split(',').Contains(s.ResourceKey))
        .Distinct().Select(t => t.ResourceKey)
        .ToList();

    public void UpdateEnabledExternalNotificationProviderList(string key, bool value)
    {
        var exists = EnabledExternalNotificationProviderList.Contains(key);

        if (value && !exists)
        {
            EnabledExternalNotificationProviderList.Add(key);
        }
        else if (!value && exists)
        {
            EnabledExternalNotificationProviderList.RemoveAll(s => s == key);
        }

        try
        {
            var config = string.Join(",", EnabledExternalNotificationProviderList.Distinct());
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationEnabled, config);
            EnabledExternalNotificationProviderCount = EnabledExternalNotificationProviderList.Count;
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"更新外部通知启用列表失败：原因={e.Message}", e);
        }
    }

    [ObservableProperty] private int _enabledExternalNotificationProviderCount = EnabledExternalNotificationProviderList.Count;

#pragma warning disable CS4014 // 由于等待不会停止
    [RelayCommand]
    private void ExternalNotificationSendTest()
        => ExternalNotificationHelper.ExternalNotificationAsync(LangKeys.ExternalNotificationTest.ToLocalization());

    [ObservableProperty] private bool _enabledCustom = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationEnableCustomMessage, false);
    partial void OnEnabledCustomChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationEnableCustomMessage, value);

    [ObservableProperty] private string _customSuccessText = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationCustomSuccessText, string.Empty);
    partial void OnCustomSuccessTextChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationCustomSuccessText, value);

    [ObservableProperty] private string _customFailureText = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationCustomFailureText, string.Empty);
    partial void OnCustomFailureTextChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationCustomFailureText, value);

    #endregion

    #region 钉钉

    [ObservableProperty] private bool _dingTalkEnabled;
    partial void OnDingTalkEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.DingTalkKey, value);

    [ObservableProperty] private string _dingTalkToken = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDingTalkToken, string.Empty);

    private static bool TryExtractDingTalkToken(string url, out string token)
    {
        token = string.Empty;

        try
        {
            if (string.IsNullOrWhiteSpace(url) && !url.Contains("access_token=", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var blocks = url.Split("access_token=");
            if (blocks.Length < 2)
            {
                return false;
            }

            token = blocks[1];
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    partial void OnDingTalkTokenChanged(string value)
    {
        if (TryExtractDingTalkToken(value, out var token))
            DingTalkToken = token;
        else
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationDingTalkToken, SimpleEncryptionHelper.Encrypt(value));
    }

    [ObservableProperty] private string _dingTalkSecret = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDingTalkSecret, string.Empty);
    partial void OnDingTalkSecretChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationDingTalkSecret, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region 邮箱

    [ObservableProperty] private bool _emailEnabled;
    partial void OnEmailEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.EmailKey, value);

    [ObservableProperty] private string _emailAccount = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationEmailAccount, string.Empty);
    partial void OnEmailAccountChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationEmailAccount, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _emailSecret = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationEmailSecret, string.Empty);
    partial void OnEmailSecretChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationEmailSecret, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region 飞书

    [ObservableProperty] private bool _larkEnabled;

    partial void OnLarkEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.LarkKey, value);
    // [ObservableProperty] private bool _larkWebhookUsed;

    [ObservableProperty] private string _larkWebhookUrl = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationLarkWebhookUrl, string.Empty);

    partial void OnLarkWebhookUrlChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationLarkWebhookUrl, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _larkId = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationLarkID, string.Empty);

    partial void OnLarkIdChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationLarkID, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _larkToken = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationLarkToken, string.Empty);
    partial void OnLarkTokenChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationLarkToken, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region 微信公众号

    [ObservableProperty] private bool _wxPusherEnabled;

    partial void OnWxPusherEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.WxPusherKey, value);

    [ObservableProperty] private string _wxPusherToken = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationWxPusherToken, string.Empty);

    partial void OnWxPusherTokenChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationWxPusherToken, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _wxPusherUid = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationWxPusherUID, string.Empty);
    partial void OnWxPusherUidChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationWxPusherUID, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region Telegram

    [ObservableProperty] private bool _telegramEnabled;

    partial void OnTelegramEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.TelegramKey, value);

    [ObservableProperty] private string _telegramBotToken = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationTelegramBotToken, string.Empty);

    partial void OnTelegramBotTokenChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationTelegramBotToken, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _telegramChatId = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationTelegramChatId, string.Empty);
    partial void OnTelegramChatIdChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationTelegramChatId, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region Discord

    [ObservableProperty] private bool _discordEnabled;

    partial void OnDiscordEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.DiscordKey, value);

    [ObservableProperty] private string _discordBotToken = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDiscordBotToken, string.Empty);
    partial void OnDiscordBotTokenChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationDiscordBotToken, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _discordChannelId = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDiscordChannelId, string.Empty);
    partial void OnDiscordChannelIdChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationDiscordChannelId, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region DiscordWebhook

    [ObservableProperty] private bool _discordWebhookEnabled;

    partial void OnDiscordWebhookEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.DiscordWebhookKey, value);

    [ObservableProperty] private string _discordWebhookUrl = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDiscordWebhookUrl, string.Empty);
    partial void OnDiscordWebhookUrlChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationDiscordWebhookUrl, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _discordWebhookName = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationDiscordWebhookName, string.Empty);
    partial void OnDiscordWebhookNameChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationDiscordWebhookName, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region SMTP

    [ObservableProperty] private bool _smtpEnabled;
    partial void OnSmtpEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.SmtpKey, value);

    [ObservableProperty] private string _smtpServer = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpServer, string.Empty);

    partial void OnSmtpServerChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpServer, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _smtpPort = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpPort, string.Empty);

    partial void OnSmtpPortChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpPort, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _smtpUser = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpUser, string.Empty);
    partial void OnSmtpUserChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpUser, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _smtpPassword = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpPassword, string.Empty);
    partial void OnSmtpPasswordChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpPassword, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _smtpFrom = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpFrom, string.Empty);
    partial void OnSmtpFromChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpFrom, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _smtpTo = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationSmtpTo, string.Empty);
    partial void OnSmtpToChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpTo, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private bool _smtpUseSsl = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationSmtpUseSsl, false);
    partial void OnSmtpUseSslChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpUseSsl, value);

    [ObservableProperty] private bool _smtpRequireAuthentication = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationSmtpRequiresAuthentication, false);
    partial void OnSmtpRequireAuthenticationChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationSmtpRequiresAuthentication, value);

    #endregion

    #region QMsg

    [ObservableProperty] private bool _qmsgEnabled;
    partial void OnQmsgEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.QmsgKey, value);

    [ObservableProperty] private string _qmsgServer = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQmsgServer, string.Empty);

    partial void OnQmsgServerChanged(string value)
        =>
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationQmsgServer, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _qmsgKey = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQmsgKey, string.Empty);

    partial void OnQmsgKeyChanged(string value)
        =>
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationQmsgKey, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _qmsgUser = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQmsgUser, string.Empty);
    partial void OnQmsgUserChanged(string value)
        =>
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationQmsgUser, SimpleEncryptionHelper.Encrypt(value));


    [ObservableProperty] private string _qmsgBot = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQmsgBot, string.Empty);
    partial void OnQmsgBotChanged(string value)
        =>
            ConfigurationManager.Current.SetValue(ConfigurationKeys.ExternalNotificationQmsgBot, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region OneBot

    [ObservableProperty] private bool _onebotEnabled;
    partial void OnOnebotEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.OneBotKey, value);

    [ObservableProperty] private string _onebotServer = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationOneBotServer, string.Empty);

    partial void OnOnebotServerChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationOneBotServer, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _onebotKey = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationOneBotKey, string.Empty);

    partial void OnOnebotKeyChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationOneBotKey, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _onebotUser = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationOneBotUser, string.Empty);
    partial void OnOnebotUserChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationOneBotUser, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region ServerChan

    [ObservableProperty] private bool _serverChanEnabled;
    partial void OnServerChanEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.ServerChanKey, value);

    [ObservableProperty] private string _serverChanSendKey = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationServerChanKey, string.Empty);
    partial void OnServerChanSendKeyChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationServerChanKey, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region CustomWebhook

    [ObservableProperty] private bool _customWebhookEnabled;
    partial void OnCustomWebhookEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.CustomWebhookKey, value);

    [ObservableProperty] private string _customWebhookUrl = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationCustomWebhookUrl, string.Empty);
    partial void OnCustomWebhookUrlChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationCustomWebhookUrl, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _customWebhookContentType = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationCustomWebhookContentType, "application/json");
    partial void OnCustomWebhookContentTypeChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationCustomWebhookContentType, SimpleEncryptionHelper.Encrypt(value));

    [ObservableProperty] private string _customWebhookPayloadTemplate = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationCustomWebhookPayloadTemplate, "{\"message\": \"{message}\"}");
    partial void OnCustomWebhookPayloadTemplateChanged(string value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationCustomWebhookPayloadTemplate, SimpleEncryptionHelper.Encrypt(value));

    #endregion

    #region QQ机器人

    [ObservableProperty] private bool _qQBotEnabled;
    partial void OnQQBotEnabledChanged(bool value) => UpdateEnabledExternalNotificationProviderList(ExternalNotificationHelper.Key.QQBotKey, value);

    [ObservableProperty] private string _qQBotAppId = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQQBotAppId, string.Empty);
    partial void OnQQBotAppIdChanged(string value)
    {
        HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotAppId, SimpleEncryptionHelper.Encrypt(value));
        QQBotVerified = false;
        UpdateQQBotStatus();
    }

    [ObservableProperty] private string _qQBotSecret = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQQBotSecret, string.Empty);
    partial void OnQQBotSecretChanged(string value)
    {
        HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotSecret, SimpleEncryptionHelper.Encrypt(value));
        QQBotVerified = false;
        UpdateQQBotStatus();
    }

    [ObservableProperty] private string _qQBotOpenId = ConfigurationManager.Current.GetDecrypt(ConfigurationKeys.ExternalNotificationQQBotOpenId, string.Empty);
    partial void OnQQBotOpenIdChanged(string value)
    {
        HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotOpenId, SimpleEncryptionHelper.Encrypt(value));
        QQBotVerified = false;
        UpdateQQBotStatus();
    }

    [ObservableProperty] private bool _qQBotVerified = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationQQBotVerified, false);
    partial void OnQQBotVerifiedChanged(bool value)
    {
        HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotVerified, value);
        UpdateQQBotStatus();
    }

    // 任务结束/错误时附带一张当前画面截图（默认开启）
    [ObservableProperty] private bool _qQBotSendImage = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationQQBotSendImage, true);
    partial void OnQQBotSendImageChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotSendImage, value);

    // 任务结束/错误时附带最近的运行日志（默认开启）
    [ObservableProperty] private bool _qQBotSendLog = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationQQBotSendLog, true);
    partial void OnQQBotSendLogChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotSendLog, value);

    // 把运行日志渲染成图片发送（规避文字消息的字数限制，默认开启）
    [ObservableProperty] private bool _qQBotLogAsImage = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationQQBotLogAsImage, true);
    partial void OnQQBotLogAsImageChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotLogAsImage, value);

    // 演出结束后附带成绩图：监听 Maadori 成绩图目录并经 QQ 机器人推送（默认关闭）
    [ObservableProperty] private bool _qQBotSendResultImage = ConfigurationManager.Current.GetValue(ConfigurationKeys.ExternalNotificationQQBotSendResultImage, false);
    partial void OnQQBotSendResultImageChanged(bool value) => HandlePropertyChanged(ConfigurationKeys.ExternalNotificationQQBotSendResultImage, value);

    [ObservableProperty] private string _qQBotStatusText = string.Empty;
    [ObservableProperty] private string _qQBotHint = string.Empty;
    [ObservableProperty] private string _qQBotOpenIdMasked = string.Empty;
    [ObservableProperty] private string _qQBotVerifyCodeInput = string.Empty;
    [ObservableProperty] private bool _isQQBotBusy;

    private string _expectedCode = string.Empty;
    private DateTime _codeExpireAt = DateTime.MinValue;

    private void UpdateQQBotStatus()
    {
        if (QQBotVerified)
        {
            QQBotStatusText = LangKeys.ExternalNotificationQQBotStatusReady.ToLocalization();
        }
        else if (!string.IsNullOrWhiteSpace(QQBotOpenId))
        {
            QQBotStatusText = LangKeys.ExternalNotificationQQBotStatusPending.ToLocalization();
        }
        else
        {
            QQBotStatusText = LangKeys.ExternalNotificationQQBotStatusUnbound.ToLocalization();
        }

        QQBotOpenIdMasked = string.IsNullOrWhiteSpace(QQBotOpenId)
            ? LangKeys.ExternalNotificationQQBotOpenIdEmpty.ToLocalization()
            : QQBotOpenId.Length <= 8 ? QQBotOpenId : QQBotOpenId.Substring(0, 8) + "…";
    }

    [RelayCommand]
    private async Task QQBotGetOpenIdAsync()
    {
        if (IsQQBotBusy)
            return;
        if (string.IsNullOrWhiteSpace(QQBotAppId) || string.IsNullOrWhiteSpace(QQBotSecret))
        {
            QQBotHint = LangKeys.ExternalNotificationQQBotCredentialInvalid.ToLocalization();
            return;
        }

        IsQQBotBusy = true;
        QQBotHint = LangKeys.ExternalNotificationQQBotCapturing.ToLocalization();
        try
        {
            var openId = await CaptureOpenIdFromGatewayAsync(QQBotAppId, QQBotSecret, TimeSpan.FromSeconds(120), CancellationToken.None);
            if (string.IsNullOrWhiteSpace(openId))
            {
                QQBotHint = LangKeys.ExternalNotificationQQBotCaptureTimeout.ToLocalization();
                return;
            }
            QQBotOpenId = openId;
            QQBotHint = LangKeys.ExternalNotificationQQBotNeedVerify.ToLocalization();
        }
        catch (Exception e)
        {
            LoggerHelper.Error($"QQ机器人获取接收地址失败：{e.Message}");
            QQBotHint = LangKeys.ExternalNotificationQQBotCredentialInvalid.ToLocalization();
        }
        finally
        {
            IsQQBotBusy = false;
        }
    }

    [RelayCommand]
    private async Task QQBotSendCodeAsync()
    {
        if (IsQQBotBusy)
            return;
        if (string.IsNullOrWhiteSpace(QQBotOpenId))
        {
            QQBotHint = LangKeys.ExternalNotificationQQBotGetOpenIdTip.ToLocalization();
            return;
        }

        IsQQBotBusy = true;
        try
        {
            _expectedCode = GenerateVerifyCode();
            _codeExpireAt = DateTime.UtcNow.AddMinutes(10);
            var content = $"[MFA] 绑定验证码：{_expectedCode}（10 分钟内有效）";
            var ok = await ExternalNotificationHelper.QQBot.SendAsync(QQBotAppId, QQBotSecret, QQBotOpenId, content, CancellationToken.None);
            QQBotHint = ok
                ? LangKeys.ExternalNotificationQQBotCodeSent.ToLocalization()
                : LangKeys.ExternalNotificationQQBotCredentialInvalid.ToLocalization();
        }
        finally
        {
            IsQQBotBusy = false;
        }
    }

    [RelayCommand]
    private void QQBotConfirm()
    {
        if (string.IsNullOrWhiteSpace(_expectedCode) || DateTime.UtcNow > _codeExpireAt
            || !string.Equals(QQBotVerifyCodeInput?.Trim(), _expectedCode, StringComparison.Ordinal))
        {
            QQBotHint = LangKeys.ExternalNotificationQQBotCodeMismatch.ToLocalization();
            return;
        }
        QQBotVerified = true;
        _expectedCode = string.Empty;
        QQBotVerifyCodeInput = string.Empty;
        QQBotHint = LangKeys.ExternalNotificationQQBotStatusReady.ToLocalization();
    }

    [RelayCommand]
    private void QQBotUnbind()
    {
        QQBotAppId = string.Empty;
        QQBotSecret = string.Empty;
        QQBotOpenId = string.Empty;
        QQBotVerifyCodeInput = string.Empty;
        QQBotVerified = false;
        _expectedCode = string.Empty;
        QQBotHint = LangKeys.ExternalNotificationQQBotStatusUnbound.ToLocalization();
    }

    private static string GenerateVerifyCode()
    {
        var bytes = new byte[4];
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes);
        var value = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 1000000;
        return value.ToString("D6");
    }

    // 平台限制：机器人只能向「曾主动给它发过消息」的用户推送，
    // 因此必须先连网关等用户发消息，才能拿到接收地址（user_openid）。
    private static async Task<string?> CaptureOpenIdFromGatewayAsync(string appId, string secret, TimeSpan timeout, CancellationToken cancellationToken)
    {
        var token = await ExternalNotificationHelper.QQBot.GetAccessTokenAsync(appId, secret, true, cancellationToken);

        string gatewayUrl;
        using (var http = VersionChecker.CreateHttpClientWithProxy())
        {
            http.Timeout = TimeSpan.FromSeconds(10);
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("QQBot", token);
            var response = await http.GetAsync("https://sandbox.api.sgroup.qq.com/gateway", cancellationToken);
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
                throw new Exception($"获取网关地址失败: {response.StatusCode} {text}");
            gatewayUrl = JObject.Parse(text)["url"]?.ToString() ?? string.Empty;
        }
        if (string.IsNullOrWhiteSpace(gatewayUrl))
            throw new Exception("获取网关地址失败: 响应缺少 url");

        using var ws = new ClientWebSocket();
        using var timeoutCts = new CancellationTokenSource();
        timeoutCts.CancelAfter(timeout);
        await ws.ConnectAsync(new Uri(gatewayUrl), timeoutCts.Token);

        var buffer = new byte[8192];
        var heartbeatCts = new CancellationTokenSource();

        try
        {
            while (!timeoutCts.IsCancellationRequested)
            {
                var result = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), timeoutCts.Token);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                JObject payload;
                try
                {
                    payload = JObject.Parse(Encoding.UTF8.GetString(buffer, 0, result.Count));
                }
                catch (Exception)
                {
                    continue;
                }

                var op = payload["op"]?.Value<int>() ?? -1;

                if (op == 10)
                {
                    var interval = Math.Max(payload["d"]?["heartbeat_interval"]?.Value<int>() ?? 41250, 1000);
                    var identify = new JObject
                    {
                        ["op"] = 2,
                        ["d"] = new JObject
                        {
                            ["token"] = $"QQBot {token}",
                            ["intents"] = 1 << 25,
                            ["shard"] = new JArray { 0, 1 },
                            ["properties"] = new JObject(),
                        },
                    };
                    await ws.SendAsync(
                        new ArraySegment<byte>(Encoding.UTF8.GetBytes(identify.ToString(Newtonsoft.Json.Formatting.None))),
                        WebSocketMessageType.Text, true, timeoutCts.Token);

                    _ = Task.Run(async () =>
                    {
                        while (!heartbeatCts.IsCancellationRequested)
                        {
                            try
                            {
                                await Task.Delay(interval, heartbeatCts.Token);
                                var ping = Encoding.UTF8.GetBytes("{\"op\":1,\"d\":null}");
                                await ws.SendAsync(new ArraySegment<byte>(ping), WebSocketMessageType.Text, true, heartbeatCts.Token);
                            }
                            catch (Exception)
                            {
                                return;
                            }
                        }
                    });
                    continue;
                }

                if (op == 0)
                {
                    if (payload["t"]?.ToString() == "READY")
                        continue;
                    var openId = FindOpenId(payload["d"]);
                    if (!string.IsNullOrWhiteSpace(openId))
                        return openId;
                }
            }
        }
        finally
        {
            heartbeatCts.Cancel();
        }
        return null;
    }

    private static string? FindOpenId(JToken? node)
    {
        if (node == null)
            return null;
        if (node is JObject obj)
        {
            foreach (var property in obj.Properties())
            {
                if (property.Name == "user_openid")
                {
                    var value = property.Value.ToString();
                    if (!string.IsNullOrWhiteSpace(value))
                        return value;
                }
            }
            foreach (var property in obj.Properties())
            {
                var found = FindOpenId(property.Value);
                if (!string.IsNullOrWhiteSpace(found))
                    return found;
            }
        }
        else if (node is JArray array)
        {
            foreach (var item in array)
            {
                var found = FindOpenId(item);
                if (!string.IsNullOrWhiteSpace(found))
                    return found;
            }
        }
        return null;
    }

    #endregion
}
