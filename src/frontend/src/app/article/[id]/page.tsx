

import Main from "./components/Main";
interface Params {
  params: { id: string };
}

export default async function ArticlePage({ params }: Params) {
 return <Main id={params.id} />;
}
