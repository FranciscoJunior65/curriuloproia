namespace CurriculosProIA.Domain.Dtos;



public class InterviewConfigDto

{

    /// <summary>Prompt para gerar o roteiro curto de apresentação em vídeo (após perguntas escritas).</summary>

    public string IntroductionPrompt { get; set; } = DefaultIntroductionPrompt;



    /// <summary>Prompt para gerar as 5 perguntas escritas com base no currículo.</summary>

    public string QuestionsPrompt { get; set; } = DefaultWrittenQuestionsPrompt;



    /// <summary>Prompt para gerar o feedback final (técnico + comportamental).</summary>

    public string FeedbackPrompt { get; set; } = DefaultFeedbackPrompt;



    public int Phase1Minutes { get; set; } = 15;

    public int Phase2Minutes { get; set; } = 10;

    public int Phase3Minutes { get; set; } = 10;

    public int MaxVideoSpeechSeconds { get; set; } = 300;

    public int MaxSegmentSeconds { get; set; } = 45;

    /// <summary>Duração máxima do vídeo de abertura (segundos).</summary>

    public int IntroMaxSeconds { get; set; } = 22;



    public const string DefaultIntroductionPrompt = """

        Você é {personaName}, {personaRole}, conduzindo entrevista por vídeo.

        Empresa/contexto: {company}

        Candidato: {candidateName}



        Gere um roteiro MUITO CURTO para o vídeo de abertura:

        - Apresente-se em UMA frase (nome + cargo)

        - Cumprimente {candidateName} pelo primeiro nome

        - Diga que em seguida ele terá tempo para se apresentar e falar sobre si

        - Tom profissional e acolhedor, português do Brasil

        - MÁXIMO {maxWords} palavras (~{introMaxSeconds} segundos de fala) — seja objetivo

        - Não mencione que é IA

        - Não faça perguntas longas nem leia o currículo



        Retorne APENAS JSON: { "script": "texto falado" }

        """;



    public const string DefaultWrittenQuestionsPrompt = """

        Você prepara uma entrevista de emprego com base no currículo abaixo.



        CONTEXTO DO CANDIDATO:

        {resumeContext}



        Gere EXATAMENTE 5 perguntas escritas para o candidato responder antes da fase em vídeo:

        - Misture perguntas técnicas (habilidades, ferramentas, projetos do CV) e comportamentais

        - Cada pergunta clara e específica ao currículo

        - Português do Brasil

        - Máximo {maxWords} palavras por pergunta

        - Sem numeração no texto da pergunta



        Retorne APENAS JSON: { "questions": ["pergunta 1", "pergunta 2", "pergunta 3", "pergunta 4", "pergunta 5"] }

        """;



    public const string DefaultFeedbackPrompt = """

        Você é {personaName}, {personaRole}. A entrevista com {candidateName} terminou.



        REGRAS OBRIGATÓRIAS:

        - Avalie o que o candidato ESCREVEU nas 5 perguntas e o que FALOU na apresentação em voz (transcrição).

        - Análise TÉCNICA: coerência das respostas escritas com o currículo, profundidade, clareza, exemplos.

        - Análise COMPORTAMENTAL: comunicação na fala livre (clareza, objetividade, postura na transcrição).

        - NÃO invente informações que não aparecem nas respostas escritas nem na transcrição falada.

        - Se não falou na apresentação ou deixou respostas vazias, diga isso claramente.

        - Score 0-100 reflete desempenho nesta simulação.



        RESPOSTAS ESCRITAS (5 perguntas):

        {writtenAnswersBlock}



        APRESENTAÇÃO EM VOZ (transcrição):

        {phase1Answer}



        RESUMO:

        {responseSummary}



        Gere feedback final:

        - Roteiro falado curto para vídeo/áudio (script)

        - Resumo escrito (overallFeedback) com seções técnica e comportamental

        - strengths e improvements baseados só no conteúdo acima

        - Português do Brasil, tom profissional e humano

        - Máximo {maxWords} palavras no script (~{maxFeedbackSeconds} segundos de fala)



        Retorne APENAS JSON:

        {

          "script": "texto falado do feedback",

          "score": 0,

          "overallFeedback": "resumo escrito",

          "strengths": ["..."],

          "improvements": ["..."]

        }

        """;

}


