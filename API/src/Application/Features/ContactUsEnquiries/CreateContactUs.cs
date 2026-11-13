using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using HKPartsEcommerce.Application.Common.Exceptions;
using HKPartsEcommerce.Application.Common.Interfaces;
using HKPartsEcommerce.Domain.Entities;
using HKPartsEcommerce.Domain.Enums;

namespace HKPartsEcommerce.Application.Features.ContactUsEnquiries;

/// <summary>Creates a product custom field. The code must be unique.</summary>
public record CreateContactUsCommand(
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string? Comments) : IRequest<ContactUsDto>;

public class CreateContactUsCommandHandler : IRequestHandler<CreateContactUsCommand, ContactUsDto>
{
    private readonly IApplicationDbContext _db;

    public CreateContactUsCommandHandler(IApplicationDbContext db) => _db = db;

    public async Task<ContactUsDto> Handle(CreateContactUsCommand request, CancellationToken cancellationToken)
    {
        if (await _db.ContactUsEnquiries.AnyAsync(f => f.Email == request.Email, cancellationToken))
            throw new ConflictException($"'{request.Email}' already exists. Please try another one.");

        var entity = new ContactUs
        {
            FirstName = request.FirstName.Trim(),
            LastName = request.LastName.Trim(),
            Email = request.Email,
            PhoneNumber = request.PhoneNumber, 
            Comments = request.Comments
        };

        _db.ContactUsEnquiries.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ContactUsDto.From(entity);
    }
}
