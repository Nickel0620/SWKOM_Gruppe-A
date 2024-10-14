using DAL.Entities;
using AutoMapper;
using REST_API.DTOs;

namespace REST_API.MappingProfiles
{
    public class DocumentProfile : Profile
    {
        public DocumentProfile()
        {
            CreateMap<Document, DocumentDTO>();
            CreateMap<DocumentDTO, Document>();
        }
    }
}
