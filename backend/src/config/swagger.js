import swaggerJsdoc from 'swagger-jsdoc';
import swaggerUi from 'swagger-ui-express';

const options = {
  definition: {
    openapi: '3.0.0',
    info: {
      title: 'CurriculosPro IA',
      version: '1.0.0',
      description: 'API para análise de currículos utilizando inteligência artificial (OpenAI GPT-4)',
      contact: {
        name: 'API Support',
        email: 'support@example.com'
      }
    },
    servers: [
      {
        url: `http://localhost:${process.env.PORT || 3000}`,
        description: 'Servidor de desenvolvimento'
      }
    ],
    components: {
      schemas: {
        AnalysisResult: {
          type: 'object',
          properties: {
            success: {
              type: 'boolean',
              example: true
            },
            originalText: {
              type: 'string',
              description: 'Texto extraído do currículo'
            },
            analysis: {
              type: 'object',
              properties: {
                pontosFortes: {
                  type: 'array',
                  items: {
                    type: 'string'
                  },
                  example: ['Experiência sólida em desenvolvimento', 'Boa formação acadêmica']
                },
                pontosMelhorar: {
                  type: 'array',
                  items: {
                    type: 'string'
                  },
                  example: ['Falta de certificações', 'Pouca experiência em liderança']
                },
                experiencia: {
                  type: 'string',
                  example: '5 anos de experiência em desenvolvimento web...'
                },
                formacao: {
                  type: 'string',
                  example: 'Graduação em Ciência da Computação...'
                },
                habilidades: {
                  type: 'array',
                  items: {
                    type: 'string'
                  },
                  example: ['JavaScript', 'React', 'Node.js', 'Python']
                },
                recomendacoes: {
                  type: 'array',
                  items: {
                    type: 'string'
                  },
                  example: ['Adicionar mais detalhes sobre projetos', 'Incluir certificações relevantes']
                },
                score: {
                  type: 'number',
                  minimum: 0,
                  maximum: 100,
                  example: 85
                }
              }
            },
            metadata: {
              type: 'object',
              properties: {
                fileName: {
                  type: 'string',
                  example: 'curriculo.pdf'
                },
                fileSize: {
                  type: 'number',
                  example: 245678
                },
                textLength: {
                  type: 'number',
                  example: 3456
                },
                processingTime: {
                  type: 'string',
                  example: '3.45s'
                }
              }
            }
          }
        },
        Error: {
          type: 'object',
          properties: {
            success: {
              type: 'boolean',
              example: false
            },
            error: {
              type: 'string',
              example: 'Erro ao processar currículo'
            },
            message: {
              type: 'string',
              example: 'Descrição detalhada do erro'
            }
          }
        }
      }
    }
  },
  apis: ['src/routes/*.js', 'src/controllers/*.js']
};

const swaggerSpec = swaggerJsdoc(options);

export const setupSwagger = (app) => {
  app.use('/api-docs', swaggerUi.serve, swaggerUi.setup(swaggerSpec, {
    customCss: '.swagger-ui .topbar { display: none }',
    customSiteTitle: 'API - CurriculosPro IA'
  }));

  // Endpoint para obter o JSON do Swagger
  app.get('/api-docs.json', (req, res) => {
    res.setHeader('Content-Type', 'application/json');
    res.send(swaggerSpec);
  });

  console.log(`📚 Swagger UI disponível em http://localhost:${process.env.PORT || 3000}/api-docs`);
};

