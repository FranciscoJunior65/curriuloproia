using CurriculosProIA.Service.Implementations;

namespace CurriculosProIA.Service.Tests;

public class ResumeLayoutHelperTests
{
    private const string SampleResumePt = """
        DADOS PESSOAIS
        João da Silva Santos
        São Paulo, SP | (11) 99999-0000 | joao@email.com | linkedin.com/in/joao

        RESUMO PROFISSIONAL
        Desenvolvedor Full Stack com 5 anos de experiência em .NET, Angular e PostgreSQL.
        Foco em APIs escaláveis e integração com sistemas legados.

        EXPERIÊNCIA PROFISSIONAL
        Tech Corp | Desenvolvedor Full Stack Sênior | 01/2021 – Atual
        - Desenvolveu APIs REST em C#/.NET 8 atendendo 50k requisições/dia
        - Implementou frontend em Angular 17 com redução de 30% no tempo de carregamento
        - Automatizou deploy com Docker e CI/CD no Azure DevOps

        Startup XYZ | Desenvolvedor Pleno | 03/2018 – 12/2020
        - Migrou monólito PHP para microsserviços Node.js
        - Integrou PostgreSQL e Redis para cache distribuído

        FORMAÇÃO ACADÊMICA
        Bacharelado em Ciência da Computação | USP | 2014 – 2017

        HABILIDADES TÉCNICAS
        - Linguagens: C#, TypeScript, Python
        - Frameworks: .NET, Angular, Node.js
        - Banco de dados: PostgreSQL, MongoDB
        """;

    private const string SampleResumeEn = """
        CONTACT
        Maria Oliveira Costa
        Rio de Janeiro, RJ | +55 21 98888-1111 | maria@email.com

        PROFESSIONAL SUMMARY
        Senior Data Engineer with expertise in Python, Spark and AWS.

        PROFESSIONAL EXPERIENCE
        Data Inc | Senior Data Engineer | 06/2019 – Present
        - Built ETL pipelines processing 2TB/day with Apache Spark
        - Reduced query latency by 45% using PostgreSQL partitioning

        EDUCATION
        BSc Computer Science | UFRJ | 2012 – 2016

        TECHNICAL SKILLS
        - Python, SQL, Spark, AWS, Docker
        """;

    [Fact]
    public void Parse_PortugueseResume_ExtractsNameAndSections()
    {
        var layout = ResumeLayoutHelper.Parse(SampleResumePt, "João da Silva Santos");

        Assert.Equal("João da Silva Santos", layout.Name);
        Assert.Contains("@", layout.Contact);
        Assert.True(layout.Sections.Count >= 4);
        Assert.Contains(layout.Sections, s =>
            s.Title.Contains("EXPERI", StringComparison.OrdinalIgnoreCase) &&
            s.Lines.Any(l => l.Contains("Tech Corp")));
    }

    [Fact]
    public void Parse_EnglishResume_ExtractsExperienceBullets()
    {
        var layout = ResumeLayoutHelper.Parse(SampleResumeEn, "Maria Oliveira Costa");

        Assert.Equal("Maria Oliveira Costa", layout.Name);
        var experience = layout.Sections.FirstOrDefault(s =>
            s.Title.Contains("EXPERIENCE", StringComparison.OrdinalIgnoreCase));
        Assert.NotNull(experience);
        Assert.True(experience!.Lines.Count >= 3);
        Assert.True(ResumeLayoutHelper.IsExperienceHeaderLine("Data Inc | Senior Data Engineer | 06/2019 – Present"));
    }

    [Fact]
    public void Parse_DoesNotTreatCityAsSectionTitle()
    {
        var text = """
            DADOS PESSOAIS
            Ana Paula Lima
            SÃO PAULO, SP | ana@mail.com

            RESUMO PROFISSIONAL
            Analista de sistemas com experiência em suporte.
            """;
        var layout = ResumeLayoutHelper.Parse(text);

        Assert.DoesNotContain(layout.Sections, s => s.Title.Equals("SÃO PAULO", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Parse_MergesBrokenDescriptionLines()
    {
        var text = """
            RESUMO PROFISSIONAL
            Profissional com experiência em desenvolvimento de software
            e integração de APIs corporativas.

            HABILIDADES TÉCNICAS
            - C#
            """;
        var layout = ResumeLayoutHelper.Parse(text);
        var resumo = layout.Sections.First(s => s.Title.Contains("RESUMO", StringComparison.OrdinalIgnoreCase));

        Assert.Single(resumo.Lines);
        Assert.Contains("integração de APIs", resumo.Lines[0]);
    }

    [Fact]
    public void IsBulletLine_RecognizesNumberedBullets()
    {
        Assert.True(ResumeLayoutHelper.IsBulletLine("1. Implementou módulo de pagamentos"));
        Assert.Equal("Implementou módulo de pagamentos",
            ResumeLayoutHelper.StripBulletPrefix("1. Implementou módulo de pagamentos"));
    }

    [Fact]
    public void GeneratePdf_LinearLayout_IncludesAllSections()
    {
        var layout = ResumeLayoutHelper.Parse(SampleResumePt, "João da Silva Santos");

        Assert.Equal("João da Silva Santos", layout.Name);
        Assert.Contains(layout.Sections, s =>
            s.Title.Contains("EXPERI", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(layout.Sections, s =>
            s.Title.Contains("HABILIDADE", StringComparison.OrdinalIgnoreCase));

        var pdf = new ResumeGeneratorService(null!, null!, null!).GenerateResumePdf(SampleResumePt, "João da Silva Santos");
        Assert.True(pdf.Length > 500);
    }

    [Fact]
    public void GeneratePdf_ProducesNonEmptyBytes()
    {
        var service = new ResumeGeneratorService(null!, null!, null!);
        var pdf = service.GenerateResumePdf(SampleResumePt, "João da Silva Santos");

        Assert.NotNull(pdf);
        Assert.True(pdf.Length > 500);
        Assert.Equal(0x25, pdf[0]);
        Assert.Equal(0x50, pdf[1]);
    }

    [Fact]
    public void GenerateDocx_ProducesNonEmptyBytes()
    {
        var bytes = ResumeDocxBuilder.BuildFromText(SampleResumePt, "João da Silva Santos");
        Assert.NotNull(bytes);
        Assert.True(bytes.Length > 500);
        Assert.Equal(0x50, bytes[0]);
        Assert.Equal(0x4B, bytes[1]);

        using var archive = new System.IO.Compression.ZipArchive(new MemoryStream(bytes));
        var entries = archive.Entries.Select(e => e.FullName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        Assert.Contains("word/document.xml", entries);

        using var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Open(new MemoryStream(bytes), false);
        Assert.NotNull(doc.MainDocumentPart?.Document?.Body);
        Assert.DoesNotContain(doc.MainDocumentPart!.Document!.Body!.InnerXml, "312E81", StringComparison.OrdinalIgnoreCase);
    }
}

public class ResumePlatformTemplatesTests
{
    [Theory]
    [InlineData("Gupy")]
    [InlineData("LinkedIn")]
    [InlineData("InfoJobs")]
    [InlineData("Indeed")]
    public void Resolve_KnownPlatforms_ReturnsSpecificProfile(string siteName)
    {
        var profile = ResumePlatformTemplates.Resolve(siteName);
        Assert.False(string.IsNullOrWhiteSpace(profile.StyleInstructionsPt));
        Assert.False(string.IsNullOrWhiteSpace(profile.StyleInstructionsEn));
    }

    [Fact]
    public void Resolve_UnknownPlatform_ReturnsDefault()
    {
        var profile = ResumePlatformTemplates.Resolve("Portal Desconhecido");
        Assert.Contains("objetiva", profile.StyleInstructionsPt, StringComparison.OrdinalIgnoreCase);
    }
}
