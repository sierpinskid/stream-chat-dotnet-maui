using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using StreamChat.Core;
using StreamChat.Core.StatefulModels;
using StreamChatMaui.Services;
using StreamChatMaui.Utils;

namespace StreamChatMaui.ViewModels;

/// <summary>
/// ViewModel for a page showing <see cref="IStreamChannel"/> details: messages, members, etc.
/// </summary>
[QueryProperty(nameof(ChannelId), QueryParams.ChannelId)]
[QueryProperty(nameof(ChannelType), QueryParams.ChannelType)]
public partial class ChannelVM : BaseViewModel, IDisposable
{
    //Todo: move to config
    public const int TitleMaxCharCount = 30;

    public string MessageInput
    {
        get => _messageInput;
        set
        {
            SetProperty(ref _messageInput, value);
            SendMessageCommand.NotifyCanExecuteChanged();
        }
    }

    public string ChannelId
    {
        set
        {
            _inputChannelId = value;
            LoadContentsAsync().LogIfFailed(_logger);
        }
    }

    public string ChannelType
    {
        set
        {
            _inputChannelType = StreamChat.Core.ChannelType.Custom(value);
            LoadContentsAsync().LogIfFailed(_logger);
        }
    }

    public string Title
    {
        get => _title;
        set => SetProperty(ref _title, value);
    }

    public IAsyncRelayCommand SendMessageCommand { get; private set; }

    public ReadOnlyObservableCollection<MessageVM> Messages { get; }

    public ChannelVM(IStreamChatService chatService, ILogger<ChannelVM> logger)
    {
        _chatService = chatService;
        _logger = logger;

        Messages = new ReadOnlyObservableCollection<MessageVM>(_messages);

        SendMessageCommand = new AsyncRelayCommand(ExecuteSendMessageCommand, CanSendMessageCommand);
    }

    private void Entry_Focused(object sender, FocusEventArgs e)
    {

    }

    public void Dispose() => UnsubscribeFromEvents();

    private readonly ObservableCollection<MessageVM> _messages = new();
    private readonly IStreamChatService _chatService;
    private readonly ILogger<ChannelVM> _logger;

    private string _inputChannelId;
    private ChannelType? _inputChannelType;

    private IStreamChannel _channel;

    private string _title = string.Empty;
    private string _messageInput = string.Empty;
    private bool _isInputFocused;
    private bool _isSending;

    private async Task ExecuteSendMessageCommand()
    {
        if (_isSending)
        {
            return;
        }

        _isSending = true;

        try
        {
            await _channel.SendNewMessageAsync(MessageInput);
            MessageInput = string.Empty;
        }
        finally
        {
            _isSending = false;
        }
    }

    private bool CanSendMessageCommand() => MessageInput?.Length > 0;

    private async Task LoadContentsAsync()
    {
        if (string.IsNullOrEmpty(_inputChannelId) || !_inputChannelType.HasValue)
        {
            return;
        }

        _logger.LogInformation($"Load channel with id: {_inputChannelId}, type: {_inputChannelType}");

        var client = await _chatService.GetClientWhenReady();
        var channel = await client.GetOrCreateChannelWithIdAsync(_inputChannelType.Value, _inputChannelId);
        SetChannel(channel);

        LoadMessages();

        Title = _channel.GenerateChannelTitle(TitleMaxCharCount);
    }

    private void LoadMessages()
    {
        foreach (var m in _channel.Messages)
        {
            AddMessage(m);
        }
    }

    //Todo: move to factory service
    private void AddMessage(IStreamMessage message) => _messages.Add(new MessageVM(message));

    private void SetChannel(IStreamChannel channel)
    {
        if (_channel != null)
        {
            UnsubscribeFromEvents();
        }

        _channel = channel;

        if (_channel == null)
        {
            return;
        }

        _channel.MessageReceived += OnMessageReceived;
        _channel.MessageUpdated += OnMessageUpdated;
        _channel.MessageDeleted += OnMessageDeleted;
    }

    private void UnsubscribeFromEvents()
    {
        if (_channel == null)
        {
            return;
        }

        _channel.MessageReceived -= OnMessageReceived;
        _channel.MessageUpdated -= OnMessageUpdated;
        _channel.MessageDeleted -= OnMessageDeleted;
    }

    private void OnMessageDeleted(IStreamChannel channel, IStreamMessage message, bool isHardDelete)
    {
        var msg = _messages.FirstOrDefault(m => m.Message == message);
        if (msg != null)
        {
            _messages.Remove(msg);
        }
    }

    private void OnMessageUpdated(IStreamChannel channel, IStreamMessage message)
    {
        var msg = _messages.FirstOrDefault(m => m.Message == message);
        msg?.Refresh();
    }

    private void OnMessageReceived(IStreamChannel channel, IStreamMessage message) => AddMessage(message);
}