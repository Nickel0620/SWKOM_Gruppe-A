//using AutoMapper;
//using DAL.Entities;
//using REST_API.DTOs;
//using REST_API.MappingProfiles;
//using Xunit;

//namespace DMS.Tests.DocumentDTOTests
//{
//    public class MappingProfileTests
//    {
//        private readonly IMapper _mapper;

//        public MappingProfileTests()
//        {
//            var config = new MapperConfiguration(cfg => cfg.AddProfile<DocumentProfile>());
//            _mapper = config.CreateMapper();
//        }

//        [Fact]
//        public void Should_Map_Document_To_DocumentDTO()
//        {
//            // Arrange
//            var document = new Document
//            {
//                Id = 1,
//                Title = "Test Document",
//                Content = "This is a test document.",
//                CreatedAt = DateTime.UtcNow,
//                UpdatedAt = DateTime.UtcNow
//            };

//            // Act
//            var documentDTO = _mapper.Map<DocumentDTO>(document);

//            // Assert
//            Assert.NotNull(documentDTO);
//            Assert.Equal(document.Id, documentDTO.Id);
//            Assert.Equal(document.Title, documentDTO.Title);
//            Assert.Equal(document.Content, documentDTO.Content);
//            // Optionally, check CreatedAt and UpdatedAt if they're in DTO
//        }

//        [Fact]
//        public void Should_Map_DocumentDTO_To_Document()
//        {
//            // Arrange
//            var documentDTO = new DocumentDTO
//            {
//                Title = "Test Document",
//                Content = "This is a test document."
//            };

//            // Act
//            var document = _mapper.Map<Document>(documentDTO);

//            // Assert
//            Assert.NotNull(document);
//            Assert.Equal(documentDTO.Title, document.Title);
//            Assert.Equal(documentDTO.Content, document.Content);
//        }
//    }
//}
