import Client from "./components/Client";
type Params = {
  params: Promise<{ id: string }>;
};

export default async function ArticlePage(props: Params) {
  const params = await props.params;
  return <Client id={params.id} />;
}
