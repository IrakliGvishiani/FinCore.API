using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.BankAccountDtos;
using FinCore.Domain.Entities;
using FinCore.Domain.Enums;
using MapsterMapper;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class AccountService : IAccountService
    {
        private readonly IRepositoryBase<BankAccount> _accounts;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IMapper _mapper;

        public AccountService(
            IRepositoryBase<BankAccount> account,
            IUnitOfWork unitOfWork,
            IMapper mapper
            )
        {
            _accounts = account;
            _unitOfWork = unitOfWork;
            _mapper = mapper;
            
        }

        public async Task CloseAccountAsync(string userId, string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.UserId == userId &&
                x.AccountNumber == accountNumber,
                tracking: false,
                cancellationToken: ct
                ) ?? throw new NotFoundException("Account not found!");

            account.Close(); //Rules are one domain

            await _unitOfWork.SaveChangesAsync();
        }

        public async Task<AccountForGettingDto> GetAccountAsync(string userId, string accountNumber, CancellationToken ct = default)
        {
            var account = await _accounts.GetAsync(
                filter: x => x.UserId == userId &&
                x.AccountNumber == accountNumber,
                tracking: false,
                cancellationToken: ct

                ) ?? throw new NotFoundException("Account not found");

            return _mapper.Map<AccountForGettingDto>(account);
        }

        public async Task<IReadOnlyList<AccountForGettingDto>> GetMyAccountsAsync(string userId, CancellationToken ct = default)
        {
            var (items, _) = await _accounts.GetAllAsync(
                filter: x => x.UserId == userId,
                orderBy: x => x.CreatedAt,
                tracking: false,
                cancellationToken: ct
                );

            return _mapper.Map<List<AccountForGettingDto>>(items);
        }

        public async Task<OpenAccountResponseDto> OpenAccountAsync(string userId, CurrencyCode currency, CancellationToken ct = default)
        {
            const int maxAttempts = 5;

            for (var attempt = 1; ; attempt++)
            {
                var account = BankAccount.Create(
                    AccountNumberGenerator.Generate(),
                    userId,
                    currency
                    );

                await _accounts.AddAsync(account,ct);

                try
                {
                    await _unitOfWork.SaveChangesAsync(ct);
                    return _mapper.Map<OpenAccountResponseDto>(account);
                }
                catch(DuplicateKeyException ex) when (attempt < maxAttempts)
                {
                    await _unitOfWork.ClearTracking();
                }
            }
        }
    }
}
