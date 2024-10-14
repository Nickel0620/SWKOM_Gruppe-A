using FluentValidation;

namespace REST_API.DTOs
{
    public class DocumentDTOValidator : AbstractValidator<DocumentDTO>
    {
        public DocumentDTOValidator()
        {
            RuleFor(doc => doc.Title)
                .NotEmpty().WithMessage("Title is required.")
                .Length(5, 100).WithMessage("Title must be between 5 and 100 characters.");

            RuleFor(doc => doc.Content)
                .MaximumLength(5000).WithMessage("Content must not exceed 5000 characters.");
        }
    }
}
