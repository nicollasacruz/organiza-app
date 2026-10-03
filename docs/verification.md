# Verificação local — 03/10/2026

- `dotnet run --project backend/Organiza.Checks --no-restore`: 68 verificações aprovadas, incluindo limites de ocupação, transporte entre meses, metas por vigência correção da meta inicial para 430 € e exclusão de agosto do transporte.
- `dotnet build backend/Organiza.Api --no-restore`: aprovado, sem avisos ou erros.
- `STATIC_EXPORT=1 npm run build`: aprovado, com TypeScript e oito páginas estáticas.
- Importação realizada pela API local, usando a sessão autenticada no Chrome: 69 registos, agosto 506,00 € e setembro 489,75 €. A API confirmou os valores calculados de cada registo. Os dados e o recibo com os identificadores ficam apenas em `.local`, ignorada pelo Git. O ficheiro de importação inclui os identificadores existentes para uma execução posterior do CLI os associar às origens sem os duplicar.
- Chrome emulação iPhone 14 Pro: 393 × 852, DPR 3, mobile e touch. Nas vistas Kanban, Semana e Mês, altura do documento de 852 px; navegação inferior começa em 780 px e o conteúdo termina em 766 px. O Kanban mostra apenas A fazer, ocupando a altura livre. O calendário semanal mantém deslocamento horizontal interno; listas longas usam deslocamento interno, sem aumentar a página.
- Chrome desktop 1440 × 900: duas colunas Kanban de 558 px, A fazer e Concluídas. Registos antigos em curso aparecem em A fazer.

Agosto é histórico e não transporta excedente. Setembro começa com saldo zero; com meta de 430 €, transporta 59,75 € para outubro. Estas contas passaram nos testes de domínio, incluindo uma alteração de agosto que não afeta setembro nem outubro. Alterações futuras pelas configurações preservam as metas dos meses anteriores.

A sessão e o shell estão agora no layout raiz através de AppFrame. A transição Tarefas → Ganhos foi verificada no Chrome: o mesmo nó `.app-shell` permanece, sem novas consultas `/api/auth/session` e sem loading de página inteira. Os dados carregados ficam no cache da sessão e são revalidados; esse cache é limpo no logout/invalidação e separado por utilizador. O primeiro carregamento mostra pulse dentro dos cards, da meta e dos registos. Para verificar, foram retidas temporariamente as chamadas reais de ganhos no browser; os quatro cards tinham `aria-busy=true` e animação loading-pulse, sem desmontar o shell. As chamadas foram depois libertadas para a API real. A animação respeita prefers-reduced-motion.

A API local também confirmou a regra nova: agosto transporta 0 €, setembro recebe 0 € e outubro recebe 59,75 €. Compilação da API aprovada, sem avisos ou erros.

BootstrapChecks com SQLite ainda não executado: falta restaurar a dependência no ambiente com acesso ao NuGet. Integração OAuth real, Docker de produção, publicação GitHub e deploy remoto continuam sem validação neste sandbox.

## Sugestões conversacionais — 03/10/2026

- Testes de domínio: 97 verificações aprovadas, incluindo disponibilidade, durações, responsáveis, ausência de sobreposição, passagem de dia e mudanças de hora em Lisboa.
- Integração OpenRouter com transporte simulado: 15 verificações aprovadas, incluindo modelo gratuito fixo, JSON estruturado, chave ausente e falhas do fornecedor.
- API compilada sem avisos ou erros; exportação estática do frontend aprovada com TypeScript e oito páginas.
- Interface verificada no Chrome com dados fictícios: conversa, resumo editável, alteração de duração, plano, alternativa local após falha e preservação da conversa ao fechar e reabrir. O layout móvel manteve o diálogo dentro da janela, sem aumentar a altura do documento. Esta verificação usou uma janela responsiva com zoom do browser; não comprova as dimensões CSS exatas do perfil iPhone.
- A chamada real ao OpenRouter e o fluxo completo com a base de dados não foram executados nesta etapa: o sandbox bloqueou DNS externo e acesso ao Docker. Os testes simulados não substituem essa validação em produção.
