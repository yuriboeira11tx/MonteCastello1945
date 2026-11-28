create table public.resultados_dossie (
  id text primary key check (id ~ '^[0-9a-f]{32}$'),
  identificacao text not null unique,
  nome_aluno text not null check (char_length(nome_aluno) between 1 and 120),
  recebido_em timestamptz not null default now(),
  concluido_em timestamptz not null,
  versao_jogo text not null,
  teste boolean not null default false,
  acertos_primeira_tentativa integer not null check (acertos_primeira_tentativa >= 0),
  perguntas_com_historico integer not null,
  perguntas_concluidas integer not null,
  total_perguntas integer not null,
  tentativas integer not null,
  erros integer not null,
  tempo_acervo_segundos double precision not null,
  tempo_acampamento_segundos double precision not null,
  tempo_reconstrucao_segundos double precision not null,
  evidencias_acervo integer not null,
  evidencias_acampamento integer not null,
  historico_incompleto boolean not null,
  tempo_incompleto boolean not null,
  respostas jsonb not null,
  relatorio jsonb not null,
  assinatura_sha256 text not null check (assinatura_sha256 ~ '^[0-9a-f]{64}$')
);
alter table public.resultados_dossie enable row level security;
revoke all on public.resultados_dossie from public, anon, authenticated;
grant select, insert on public.resultados_dossie to service_role;
comment on table public.resultados_dossie is 'Relatórios finais assinados do Monte Castello 1945. Leitura apenas pela administração; envio pela função receber-relatorio.';
create index resultados_dossie_recebido_em_idx on public.resultados_dossie (recebido_em desc);
