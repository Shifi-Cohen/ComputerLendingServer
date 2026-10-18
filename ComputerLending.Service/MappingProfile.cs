using AutoMapper;
using ComputerLending.Core.DTOs.Computers;
using ComputerLending.Core.DTOs.Loans;
using ComputerLending.Core.DTOs.Tags;
using ComputerLending.Core.Entities;

namespace ComputerLending.Service;

public class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Tag, TagResponse>();
        CreateMap<CreateTagRequest, Tag>();
        CreateMap<Computer, ComputerResponse>();
        CreateMap<CreateComputerRequest, Computer>();
        CreateMap<Loan, LoanResponse>()
            .ForMember(d => d.ComputerName, o => o.MapFrom(s => s.Computer.Name))
            .ForMember(d => d.AssetTag, o => o.MapFrom(s => s.Computer.AssetTag))
            .ForMember(d => d.UserName, o => o.MapFrom(s => s.User.FullName));
    }
}
