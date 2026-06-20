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

        Você é uma entrevistadora de RH conduzindo entrevista por vídeo.

        CONTEXTO INTERNO (não fale em voz alta): {resumeContext}



        Gere um roteiro MUITO CURTO para o vídeo de abertura:

        - Comece com saudação genérica (ex.: "Olá!" ou "Olá, bem-vindo")

        - NÃO use o nome do candidato

        - NÃO diga seu nome pessoal nem o nome da empresa

        - Pode dizer apenas que é da equipe de RH / entrevistadora

        - Diga que em seguida o candidato terá tempo para se apresentar

        - Tom profissional e acolhedor, português do Brasil

        - MÁXIMO {maxWords} palavras (~{introMaxSeconds} segundos de fala)

        - Não mencione que é IA

        - Não faça perguntas longas nem leia o currículo



        Retorne APENAS JSON: { "script": "texto falado" }

        """;



    public const string DefaultWrittenQuestionsPrompt = """

        Você prepara uma entrevista de emprego com base no currículo abaixo.



        CONTEXTO DO CANDIDATO:

        {resumeContext}



        Gere EXATAMENTE 5 perguntas para o candidato responder antes da fase em vídeo:

        - EXATAMENTE 3 perguntas de múltipla escolha (type: "choice") com 4 alternativas plausíveis cada

        - EXATAMENTE 2 perguntas abertas (type: "open") para resposta em texto livre

        - Misture perguntas técnicas (habilidades, ferramentas, projetos do CV) e comportamentais

        - Cada pergunta clara e específica ao currículo

        - Português do Brasil

        - Máximo {maxWords} palavras no texto da pergunta

        - Sem numeração no texto da pergunta



        Retorne APENAS JSON:

        {

          "questions": [

            { "text": "pergunta", "type": "choice", "options": ["alt A", "alt B", "alt C", "alt D"] },

            { "text": "pergunta aberta", "type": "open", "options": [] }

          ]

        }

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


