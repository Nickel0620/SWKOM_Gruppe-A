//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;
//using FluentValidation.TestHelper;
//using REST_API.DTOs;
//using Xunit;

//namespace DMS.Tests.DocumentDTOTests
//{
//    public class DocumentDTOValidatorTests
//    {
//        private readonly DocumentDTOValidator _validator;

//        public DocumentDTOValidatorTests()
//        {
//            _validator = new DocumentDTOValidator();
//        }

//        [Fact]
//        public void Should_Have_Error_When_Title_Is_Empty()
//        {
//            // Arrange
//            var model = new DocumentDTO { Title = "" };

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldHaveValidationErrorFor(doc => doc.Title)
//                  .WithErrorMessage("Title is required.");
//        }

//        [Fact]
//        public void Should_Not_Have_Error_When_Title_Is_Valid()
//        {
//            // Arrange
//            var model = new DocumentDTO { Title = "Valid Title" };

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldNotHaveValidationErrorFor(doc => doc.Title);
//        }

//        [Fact]
//        public void Should_Have_Error_When_Title_Is_Too_Short()
//        {
//            // Arrange
//            var model = new DocumentDTO { Title = "abc" }; // less than 5 characters

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldHaveValidationErrorFor(doc => doc.Title)
//                  .WithErrorMessage("Title must be between 5 and 100 characters.");
//        }

//        [Fact]
//        public void Should_Have_Error_When_Title_Is_Too_Long()
//        {
//            // Arrange
//            var model = new DocumentDTO { Title = new string('a', 101) }; // more than 100 characters

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldHaveValidationErrorFor(doc => doc.Title)
//                  .WithErrorMessage("Title must be between 5 and 100 characters.");
//        }

//        [Fact]
//        public void Should_Have_Error_When_Content_Exceeds_MaxLength()
//        {
//            // Arrange
//            var model = new DocumentDTO { Content = new string('a', 6000) }; // more than 5000 characters

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldHaveValidationErrorFor(doc => doc.Content)
//                  .WithErrorMessage("Content must not exceed 5000 characters.");
//        }

//        [Fact]
//        public void Should_Not_Have_Error_When_Content_Is_Valid()
//        {
//            // Arrange
//            var model = new DocumentDTO { Content = new string('a', 5000) }; // exactly 5000 characters

//            // Act
//            var result = _validator.TestValidate(model);

//            // Assert
//            result.ShouldNotHaveValidationErrorFor(doc => doc.Content);
//        }
//    }
//}
