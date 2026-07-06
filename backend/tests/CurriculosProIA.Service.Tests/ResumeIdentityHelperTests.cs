using CurriculosProIA.Service.Implementations;

namespace CurriculosProIA.Service.Tests;

public class ResumeIdentityHelperTests
{
    private const string MarcosOriginal = """
        Marcos Vinicius Oliveira Lima
        Sorocaba, SP | 55+ (15) 98102-9870 | Linkedin | Github
        HABILIDADES
        C#
        Angular
        Web Api RESTFull
        EXPERIÊNCIA
        Tafner Software SolutionsSorocaba, SP
        Desenvolvedor Full-StackOut 2023 - Ago 2024
        Desenvolver e manter páginas com alto fluxo de usuários, utilizando Angular, JavaScript e .Net
        """;

    [Fact]
    public void Extract_MarcosResume_ReturnsCorrectNameAndContact()
    {
        var identity = ResumeIdentityHelper.Extract(MarcosOriginal);

        Assert.Equal("Marcos Vinicius Oliveira Lima", identity.Name);
        Assert.Contains("Sorocaba", identity.ContactLine ?? string.Empty);
        Assert.Contains("C#", identity.Skills);
    }

    [Fact]
    public void EnforceFidelity_ReplacesWrongNameWithOriginal()
    {
        var identity = ResumeIdentityHelper.Extract(MarcosOriginal);
        var wrong = """
            DADOS PESSOAIS
            Francisco Alves Fernandes Junior
            Sorocaba, SP | marcos.vinicius.oliveira.lima@email.com | linkedin.com/in/teste

            RESUMO PROFISSIONAL
            Desenvolvedor Full-Stack com experiência em arquitetura.

            IDIOMAS
            Português: Nativo
            Inglês: (Nível não especificado no original)
            """;

        var fixedText = ResumeIdentityHelper.EnforceFidelity(wrong, identity, MarcosOriginal);

        Assert.Contains("Marcos Vinicius Oliveira Lima", fixedText);
        Assert.DoesNotContain("Francisco Alves Fernandes Junior", fixedText);
        Assert.DoesNotContain("@email.com", fixedText);
        Assert.DoesNotContain("IDIOMAS", fixedText);
    }

    [Fact]
    public void BuildIdentityPromptBlock_IncludesVerifiedName()
    {
        var identity = ResumeIdentityHelper.Extract(MarcosOriginal);
        var block = ResumeIdentityHelper.BuildIdentityPromptBlock(identity);

        Assert.Contains("Marcos Vinicius Oliveira Lima", block);
        Assert.Contains("PROIBIDO", block);
    }
}
