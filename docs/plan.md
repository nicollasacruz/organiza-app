# Plano aprovado

## Base

Organiza é uma PWA fullscreen privada de família. .NET 10, ASP.NET Core Identity com palavras-passe e passkeys nativas, EF Core/Npgsql, PostgreSQL 18 e Next.js 16 com exportação estática. Todos os membros ativos editam ganhos e tarefas. Administrador cria convites de sete dias ligados ao email, uso único. Login Google não substitui a autenticação da app.

Desenvolvimento usa dotnet run e next dev; Docker Compose apenas para PostgreSQL. Produção usa Compose com monólito a servir API e frontend estático, HTTPS, PostgreSQL privado e chaves Data Protection persistentes. Não alterar stack sem a opinião do utilizador.

## Ganhos

SALA: 7 €/hora, incluindo frações. AULA 30M: até 49% = 7,50 €; acima de 49% e abaixo de 90% = 9,75 €; desde 90% = 10,60 €. AULA 50M: 13 €, 16 €, 17,50 € nas mesmas faixas. Ocupação não é arredondada antes de escolher faixa. Zero pessoas recebe o valor base. Lotação positiva, presenças inteiras até à lotação. Dinheiro decimal no cálculo e cêntimos inteiros no armazenamento/API.

Meta mensal inicial de 430 € (corrigida retroativamente a pedido do utilizador em 03/10/2026), ajustável nas configurações e partilhada pela família. Uma alteração só tem efeito a partir do mês atual em Lisboa; as metas dos meses anteriores são preservadas. Guardar novamente no mesmo mês atualiza apenas esse mês e os seguintes. O histórico de vigências determina a meta de cada mês. Disponível = ganho real + saldo anterior. Transporte = max(0, disponível − meta). Falta = max(0, meta − disponível). Meses vazios também entram no cálculo, sem dívida negativa. Retroedições recalculam meses seguintes. Agosto de 2026 é histórico e não transporta excedente para setembro. Setembro começa com saldo zero; o transporte inicia-se de setembro para outubro. Os dados reais são importados de fontes privadas e não publicados. A primeira data malformada do Excel foi confirmada como 1 de setembro de 2026. Linhas iguais são preservadas, e identificadores de origem tornam a importação repetida idempotente.

## Análise

Módulo separado em /analise, disponível aos membros ativos. Análise descritiva dos registos de ganhos, com vista mensal (seis meses) e semanal (oito semanas, segunda a domingo), navegação histórica e filtros Todos/SALA/AULA 30M/AULA 50M. Datas de Lisboa e histórico desde agosto de 2026. Os cálculos não incluem datas futuras. Meses/semanas vazios dentro do histórico são mantidos, sem assumir que ausência de registos prova ausência de trabalho.

Ganho real é a soma dos cêntimos registados, sem transporte. Cada registo de aula é uma sessão, mesmo com zero pessoas; registos iguais são preservados. Presenças são participações, não alunos únicos. Média de pessoas por aula = soma das presenças / número de sessões. Ocupação ponderada = soma das presenças / soma das lotações × 100, sem arredondar os valores intermediários. Sem aulas, média e ocupação são indisponíveis. Variação percentual = (atual − anterior) / anterior × 100, indisponível quando a base é zero ou não existe; mudança de ocupação em pontos percentuais. Não aplicar benchmarks de SaaS, retenção ou afirmações causais a estes registos.

Períodos fechados comparam com o período anterior inteiro. Períodos correntes comparam apenas até ao mesmo dia do mês/semana anterior; meses anteriores mais curtos terminam no último dia. Mostrar os intervalos exatos e identificar períodos parciais. Não inventar histórico anterior à abertura.

Resumo global do mês atual independente dos filtros: ganho até hoje, saldo anterior, disponível, meta vigente e falta, preservando as regras dos ganhos. Estimativa de fecho = ganho registado até à data de corte / dias decorridos × dias do mês; arredondar apenas o resultado final para cêntimos. Disponível estimado = estimativa + saldo anterior. Sem registos, não estimar. É uma extrapolação simples do ritmo diário, não uma garantia nem um modelo ajustado ao calendário de trabalho.

Reutilizar APIs existentes e snapshots privados por utilizador. Offline, mostrar a data de sincronização dos ganhos e dos saldos, limitar a análise à data do snapshot e distinguir dados ausentes de totais zero. Se o snapshot de ganhos for de um mês anterior, não estimar o mês atual. Gráficos SVG com interação por toque/rato/teclado e alternativa em tabela; sem novas dependências, tabelas ou migrações. A navegação móvel tem Ganhos, Análise, Tarefas, Definições e Membros; a saída passa para Definições, mantendo o perfil desktop.

## Tarefas

Kanban no computador: A fazer / Concluídas; Concluídas mostra apenas a ocorrência concluída mais recente de cada série, ordenada por conclusão mais recente, e o contador corresponde aos cartões visíveis. Tarefas avulsas permanecem individuais. O histórico completo é preservado nos dados e nos calendários; no mobile: apenas A fazer, preenchendo o espaço livre. Registos antigos em curso aparecem em A fazer; vista semanal de segunda a domingo e vista mensal. Responsável opcional, descrição opcional; data opcional em tarefas avulsas e obrigatória na primeira recorrência.

Recorrências a partir da conclusão: diária 1 dia, cada 3 dias, semanal 7, quinzenal 15, mensal 30, bimestral 60, trimestral 90, semestral 180. Personalizada N dias / N×7 semanas / N×30 meses. Não usar AddMonths para recorrências.

Uma ocorrência ativa real por rotina. Ao concluir, preservar o histórico e criar a próxima com data de conclusão em Lisboa + intervalo. Pendente atrasada mantém a data original. Transação e bloqueio de linha impedem duas próximas ocorrências. Desfazer só a última conclusão, se a próxima não foi alterada, iniciada ou copiada para Google. Eliminar a ativa termina a rotina e conserva o histórico.

No Kanban, A fazer mostra tarefas sem data, atrasadas e datadas até hoje+7 dias (inclusive); as posteriores continuam acessíveis nos calendários. As atrasadas permanecem até serem concluídas. As colunas preenchem a altura útil, sem scroll da página; listas extensas têm scroll interno. Atrasadas ficam vermelhas, hoje até hoje+3 dias (inclusive) âmbar. Rótulos acompanham as cores. Concluídas não ficam atrasadas. Os calendários mostram previsões só de leitura. Se a atual está atrasada, prever a partir de hoje; caso contrário, a partir da data atual prevista. Previsões assumem conclusão na data prevista, não têm ID real e nunca podem ser concluídas ou copiadas para Google.

## Google Agenda

OAuth individual separado do login, seleção de calendário com acesso de escrita incluindo partilhados. Guardar uma cópia manual de uma ocorrência real, dia inteiro ou hora/duração em Lisboa. Nunca exportar recurrence ou previsões. Alterar, concluir ou eliminar uma tarefa não muda a cópia. Nova ocorrência exige nova ação.

Tokens encriptados no servidor; estado OAuth ligado ao membro, cookie de nonce e PKCE. Scopes calendar.calendarlist.readonly e calendar.events. ID determinístico válido de evento e registo por ocorrência/membro/conta/calendário evitam duplicação em repetição ou falhas parciais. Desligar remove tokens e mantém as cópias independentes.

## Tema e offline

Paleta padrão rosa pastel (#e6b8c5), com opções azul, violeta, terracota, cor primária customizada; claro, escuro ou sistema por utilizador. Contraste do texto do botão calculado. Urgência mantém semântica própria. Português, euros, Lisboa.

Service worker apenas em produção e só para ficheiros estáticos. Dados privados em IndexedDB por utilizador e leitura offline com data de sincronização. Logout, sessão expirada ou acesso revogado limpam os dados. Em next dev, desativar service worker. PWA real local servida pelo dotnet run após exportação Next.
