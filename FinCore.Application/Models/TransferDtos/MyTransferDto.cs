using FinCore.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Models.TransferDtos
{
    public enum TransferDirection { Outgoing,Incoming}
    public class MyTransferDto
    {
        public int Id { get; set; }

        public TransferDirection Direction { get; set; }

        public string CounterPartyAccountNumber { get; set; }

        public string MyAccountNumber { get; set; }

        public decimal Amount { get; set; }

        public CurrencyCode Currency { get; set; }

        public TransferStatus Status { get; set; }

        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; }
    }
}
