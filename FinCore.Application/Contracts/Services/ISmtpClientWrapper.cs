using MimeKit;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface ISmtpClientWrapper
    {
        Task ConnectAsync(string host, int port, bool useSsl);

        Task AuthenticateAsync(string userName, string passWord);

        Task SendAsync(MimeMessage message);
        Task DisconnectAsync(bool quit);
    }
}
