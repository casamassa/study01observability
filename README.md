# study01observability

Subir os containers:

Prometheus:

docker run -d --name prometheus -p 9090:9090 -v "${pwd}/docker/prometheus.yml:/etc/prometheus/prometheus.yml" prom/prometheus

Loki:

docker run -d --name loki -p 3100:3100 grafana/loki

Tempo

docker run -d --name tempo -p 3200:3200 -p 4317:4317 -v "${pwd}/docker/tempo-config.yaml:/etc/tempo.yml" grafana/tempo:2.3.1 --config.file=/etc/tempo.yml --target=all

Grafana:

docker run -d --name grafana -p 3000:3000 grafana/grafana

---

GRAFANA

1 - Configure a Fonte de Dados no Grafana:
1.1 - Acesse http://localhost:3000 (user/pass: admin/admin).
Vá em Connections > Data Sources > Add data source.
Selecione Prometheus. -> Na URL, coloque http://host.docker.internal:9090. -> Clique em Save & Test
Selecione Loki. -> Na URL, coloque http://host.docker.internal:3100. -> Clique em Save & Test
Selecione Tempo. -> Na URL, coloque http://host.docker.internal:3200. -> Clique em Save & Test

2 - No Grafana, configure a Ponte Final (Data Link)
Para o Grafana entender que aquele texto do TraceId é um link para o Tempo, faça o seguinte:
1 Vá em Connections > Data Sources > Loki.
2 Role até a seção Derived Fields.
3 Clique em + Add.
3.1 Name: TraceID
3.2 Regex: (?:TraceId|traceId|trace_id)":"(\w+)" (Isso extrai o ID do texto do log).
3.3 Query: ${\_\_value.raw}
3.4 Internal Link: Ative e selecione o Data Source Tempo.
4 Clique em Save & Test

3 - Bora montar o seu Dashboard de Visão 360°! O objetivo é ter as métricas no topo e os logs logo abaixo, tudo filtrado pelo mesmo intervalo de tempo.

3.1. Criar o Dashboard e o Gráfico de Métricas (Prometheus)
No Grafana, vá em Dashboards -> New -> New Dashboard.
Clique em + Add Visualization.
Selecione o data source Prometheus.
No campo Query (PromQL), cole:
promql
rate(http_requests_received_total{application="my-api-dotnet"}[1m])
Use o código com cuidado.

No painel lateral direito (Panel options):
Title: Requisições por Segundo (RPS)
Graph styles: Mude para Line ou Area.
Clique em Apply (topo direito).

3.2. Adicionar o Painel de Logs (Loki)
Clique no ícone de + (Add) no topo do dashboard e escolha Visualization.
Selecione o data source Loki.
No campo Query (LogQL), cole:
logql
{application="my-api-dotnet"} |= ``
Use o código com cuidado.

No painel lateral direito:
Title: Logs em Tempo Real
Visualization: Procure por Logs (em vez de Time Series).
Clique em Apply.

3.3. Organizar e Salvar
Arraste o painel de Logs para ficar abaixo do gráfico de métricas.
Redimensione os painéis para ocuparem toda a largura da tela.
Clique no ícone de Disco Rígido (Save dashboard) no topo e dê o nome: Observabilidade .NET 10.

---

LOKI

- Verificar se o Loki recebeu "Labels":

URL: http://localhost:3100/loki/api/v1/labels

O que esperar: Um JSON contendo "values": ["application", ...].

Se estiver vazio: O .NET não conseguiu enviar nada para o Loki.

- Consultar os logs via API (O "Select" do Loki)

http://localhost:3100/loki/api/v1/query_range?query={application="my-api-dotnet"}

Se retornar values com textos: Os logs estão no Loki! O problema é apenas a visualização no Grafana.

Se retornar vazio: Os logs não saíram da API.

---

TEMPO

Cole o ID no final desta URL no seu navegador para testar se o tempo recebeu:

http://localhost:3200/api/traces/COLE_AQUI_O_ID

---

Comandos importantes do Docker:

- Reiniciar um container:
  docker restart prometheus
- Parar um container:
  docker stop prometheus
- Apagar um container:
  docker rm -f prometheus
