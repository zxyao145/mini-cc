"use client";

import { useEffect, useState } from "react";
import TagDetail from "@/components/TagDetail";

interface TagDetailPageProps {
  params: Promise<{
    id: string;
  }>;
}

export default function TagDetailPage({ params }: TagDetailPageProps) {
  const [id, setId] = useState<string>('');
  
  useEffect(() => {
    params.then(p => setId(p.id));
  }, [params]);
  
  if (!id) return null;
  
  return <TagDetail tagId={id} />;
}