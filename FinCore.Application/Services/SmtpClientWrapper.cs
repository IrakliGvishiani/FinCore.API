using FinCore.Application.Contracts.Services;
using MimeKit;
using System;
using System.Collections.Generic;
using MailKit.Net.Smtp;

using System.Text;

namespace FinCore.Application.Services
{
    public class SmtpClientWrapper : ISmtpClientWrapper
    {
        private readonly SmtpClient _client = new();
        public async Task AuthenticateAsync(string userName, string passWord) => await _client.AuthenticateAsync(userName, passWord);


        public async Task ConnectAsync(string host, int port, bool useSsl) => await _client.ConnectAsync(host, port, useSsl);

        public async Task SendAsync(MimeMessage message) => await _client.SendAsync(message);

        public async Task DisconnectAsync(bool quit) => await _client.DisconnectAsync(quit);

        public void Dispose() => _client.Dispose();
    }
}
