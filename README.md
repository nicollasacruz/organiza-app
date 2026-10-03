# Organiza

PWA privada para os ganhos e tarefas da família. Frontend Next.js 16; monólito ASP.NET Core 10 com Identity, passkeys, EF Core e PostgreSQL 18. O backend serve o frontend estático em produção.

## Abrir localmente

Requisitos: .NET SDK 10, Node.js 24 e Docker/OrbStack. Execute os comandos na raiz deste projeto.

```sh
cp .env.example .env
# Edite .env: configure uma DEV_DB_PASSWORD longa e aleatória.
docker compose -f compose.dev.yml up -d
./scripts/api.sh init-db
./scripts/api.sh bootstrap seu@email.pt 'O seu nome'
```

O comando bootstrap prepara automaticamente o esquema inicial, se necessário, e pede uma palavra-passe sem a mostrar. É a única forma de criar o primeiro administrador. Não existe uma conta ou palavra-passe predefinida. O comando exige uma base de dados vazia de utilizadores e preserva uma conta já criada ao rejeitar a repetição.

No primeiro terminal:

```sh
./scripts/api.sh
```

No segundo terminal:

```sh
cd frontend
npm install
npm run dev
```

Abra **http://localhost:3000/ganhos/** e entre com a conta criada. A API escuta na porta 5080. O script api.sh executa `dotnet run`, e npm run dev executa `next dev`; apenas a base de dados fica em Docker durante o desenvolvimento. Use localhost em vez de 127.0.0.1 para passkeys, pois o RP ID é localhost.

## Importar a planilha privada

Os dados financeiros não fazem parte deste repositório público. O script converte as 34 linhas de agosto e as 35 linhas de setembro do ficheiro indicado, verifica os valores e corrige a data inicial de setembro, conforme confirmado.

```sh
python3 scripts/prepare-import.py '/caminho/Ganhos Trabalho.xlsx' --tsv .local/august.tsv --first-date 2026-09-01
./scripts/api.sh import .local/earnings-import.json
```

A importação preserva aulas iguais e não duplica a mesma linha de origem quando repetida. Os totais esperados são 506,00 € em agosto e 489,75 € em setembro; agosto não transporta saldo para setembro; setembro começa com saldo zero e transporta 59,75 € para outubro, com meta de 430 €. Os dados ficam exclusivamente em .local e na base de dados.

## Regras e verificações

```sh
dotnet run --project backend/Organiza.Checks
dotnet run --project backend/Organiza.BootstrapChecks
cd frontend
npm run typecheck
STATIC_EXPORT=1 npm run build
```

As regras acordadas estão em [docs/plan.md](docs/plan.md). A skill indicada pelo utilizador está em [skills/karpathy-guidelines/SKILL.md](skills/karpathy-guidelines/SKILL.md), com origem no [repositório multica-ai](https://github.com/multica-ai/andrej-karpathy-skills).

A API e `init-db` preparam o esquema sem eliminar dados, incluindo a tabela de metas mensais para instalações existentes. A meta inicial é 430 €; pode alterá-la nas configurações com vigência desde o mês atual, preservando os meses anteriores. Reinicie a API depois de atualizar o código. Outras alterações futuras de esquema exigem migrações; não elimine volumes para atualizar a aplicação. Faça backups do PostgreSQL e das chaves de Data Protection.

## Testar a PWA real localmente

```sh
./scripts/preview.sh
```

Este script exporta o Next.js e serve o resultado com `dotnet run`. Abra http://localhost:5080/ganhos/. O service worker funciona apenas neste build de produção, não em next dev. Para testar o offline, entre, visite os ecrãs e ative Offline nas ferramentas do navegador. Os dados sincronizados ficam apenas para leitura; terminar sessão limpa os dados privados. A instalação fullscreen depende do suporte do sistema, com fallback standalone. A integração Google no preview precisa de RedirectUri e PublicOrigin ajustados para localhost:5080.

## Google Agenda

No Google Cloud, ative Calendar API e crie um cliente OAuth do tipo Web. Configure o consentimento e os utilizadores de teste. Coloque GOOGLE_CLIENT_ID e GOOGLE_CLIENT_SECRET no .env local. Registe os redirects exatos:

- Desenvolvimento: http://localhost:3000/api/integrations/google/callback
- Produção: https://SEU-DOMINIO/api/integrations/google/callback

A ligação é individual e separada do login. Scopes: calendar.calendarlist.readonly e calendar.events. Os tokens ficam encriptados no servidor. Pode escolher calendários partilhados com acesso de escrita. Só ocorrências reais podem ser copiadas. Nenhum evento contém recurrence; não há sincronização posterior. Repetir o envio da mesma ocorrência para a mesma conta/calendário devolve a cópia existente.

## Produção em Docker Compose

Destino: root@62.169.28.198, pasta /root/projects/organiza-app e domínio https://appcasa.run.place. O servidor deve ter a rede Docker externa reverse-proxy e o domínio deve apontar para esse servidor. As labels do Compose ativam a rota no Traefik, no entrypoint websecure, com o resolvedor ACME le-http (TLS-ALPN-01) já configurado no servidor. O Traefik termina o HTTPS e encaminha para a porta interna 8080 da app. O redirecionamento HTTP para HTTPS já está configurado no entrypoint web do proxy.

O .env privado contém PUBLIC_ORIGIN, PASSKEY_DOMAIN, VIRTUAL_HOST e POSTGRES_PASSWORD. O email e o armazenamento dos certificados pertencem à configuração do Traefik; as variáveis antigas LETSENCRYPT_HOST/LETSENCRYPT_EMAIL não são usadas pela app. As palavras-passe de desenvolvimento e produção devem ser diferentes. Google continua opcional até configurar as suas credenciais. Não publique ou coloque o .env no GitHub.

No servidor, dentro de /root/projects/organiza-app:

```sh
./scripts/deploy.sh
docker compose run --rm app bootstrap seu@email.pt 'O seu nome'
```

O script deteta e guarda a subnet da rede do proxy, valida o Compose, constrói as imagens, prepara o esquema e arranca os serviços. A app aceita cabeçalhos encaminhados apenas dessa rede e de loopback; isto permite cookies HTTPS, passkeys e OAuth atrás do proxy. O terminal pede a palavra-passe inicial.

A aplicação corre como utilizador sem privilégios e serve frontend/API na mesma origem. A porta interna 8080 é acessível pelo proxy e não é publicada no host. PostgreSQL fica numa rede própria e não expõe portas. Volumes separados persistem dados e chaves. Os convites usam PUBLIC_ORIGIN.

Para recuperar uma conta sem SMTP, execute no servidor:

```sh
docker compose run --rm app reset-password seu@email.pt
```

## Limites da validação desta sessão

Consulte [docs/verification.md](docs/verification.md) para os comandos executados, resultados e verificações ainda dependentes de acesso a Docker, rede e credenciais Google.
