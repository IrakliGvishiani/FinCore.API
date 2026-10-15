using FinCore.Application.Models.Notification;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Contracts.Services
{
    public interface IEmailService
    {
        Task<SendEmailResponse> Send(string to,string subject,string body);
    }
}
