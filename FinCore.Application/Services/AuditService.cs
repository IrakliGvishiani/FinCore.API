using FinCore.Application.Contracts.Persistence;
using FinCore.Application.Contracts.Services;
using FinCore.Application.Exceptions;
using FinCore.Application.Models.Audit;
using FinCore.Domain.Entities;
using MapsterMapper;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Text;

namespace FinCore.Application.Services
{
    public class AuditService : IAuditService
    {
        private readonly IRepositoryBase<AuditLog> _auditRepository;
        private readonly IUnitOfWork _unitOfWork;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly IRequestContext _request;
        private readonly ILogger<AuditLog> _logger;
        private readonly IMapper _mapper;

        public AuditService(
            IRepositoryBase<AuditLog> auditRepo,
            IUnitOfWork unitOfWork,
            ILogger<AuditLog> logger,
            IRequestContext request,
            IServiceScopeFactory serviceScopeFactory,
            IMapper mapper
            )
        {
            _auditRepository = auditRepo;
            _unitOfWork = unitOfWork;
            _logger = logger;
            _request = request;
            _scopeFactory = serviceScopeFactory;
            _mapper = mapper;
        }

        public async Task RecordAsync(
         string? userId, string action, string entityName,
         string? entityId = null, string? details = null, CancellationToken ct = default
            )
        {
            var log = AuditLog.Create(userId,action,entityName,entityId,details,_request.IpAddress);
            await _auditRepository.AddAsync(log,ct);
        }

        public async Task LogAsync(string? userId,
            string action, string entityName,
            string? entityId = null,
            string? details = null)
        {
            try
            {
                var log = AuditLog.Create(userId,action,entityName,entityId,details,_request.IpAddress);

                using var scope = _scopeFactory.CreateScope();
                var repo = scope.ServiceProvider.GetRequiredService<IRepositoryBase<AuditLog>>();
                var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

                await repo.AddAsync(log);
                await uow.SaveChangesAsync(CancellationToken.None);
            }
            catch(Exception ex)
            {
                _logger.LogError(ex,"Audit Write Failed: {Action} {EntityName}",action,entityName);
            }
        }

        public async Task<(IEnumerable<AuditLogDto> Items, int TotalCount)> GetLogsAsync(AuditFilter filter,
            int pageNumber = 1, int pageSize = 20, CancellationToken ct = default)
        {
            if (pageNumber < 1)
                throw new BadRequestException("Page number must be greater than zero.");
            if (pageSize < 1 || pageSize > 100)
                throw new BadRequestException("Page size must be between 1 and 100.");
            if (filter.FromDate > filter.ToDate)
                throw new BadRequestException("FromDate cannot be after ToDate.");

            var userId = filter.UserId?.Trim();
            var action = filter.Action?.Trim();
            var entityName = filter.EntityName?.Trim();
            var entityId = filter.EntityId?.Trim();
            var ip = filter.IpAddress?.Trim();
            var fromDate = filter.FromDate?.Date;
            var toExclusive = filter.ToDate?.Date.AddDays(1);

            var (items, total) = await _auditRepository.GetAllAsync(
                filter: a => 
                (userId == null || a.UserId == userId) &&
                (action == null || a.Action.StartsWith(action)) &&
                (entityName == null || a.EntityName == entityName) &&
                (entityId == null || a.EntityId == entityId) &&
                (ip == null || a.IpAddress == ip) &&
                (fromDate == null || a.CreatedAt >= fromDate) &&
                (toExclusive == null || a.CreatedAt < toExclusive),

                pageNumber,
                pageSize,
                orderBy: a => a.Id,
                ascending: false,
                tracking: false,
                cancellationToken: ct
                );

            var mappedItems = _mapper.Map<List<AuditLogDto>>(items);

            return (mappedItems, total);
        }
    }
}
