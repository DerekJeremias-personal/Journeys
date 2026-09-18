"use client";

/**
 * TemplatesPicker — grid of starter templates with a category filter row.
 * On selection, calls `onSelect(template.createCampaign())` to hydrate the
 * wizard form.
 */

import { useMemo, useState } from "react";

import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { ToggleGroup, ToggleGroupItem } from "@/components/ui/toggle-group";
import type { Campaign } from "@/lib/campaign-types";

import {
  WIZARD_TEMPLATES,
  WIZARD_TEMPLATE_CATEGORIES,
  type CampaignTemplate,
  type TemplateCategory,
  type TemplateTone,
} from "../wizard-templates";

const TONE_CLASS: Record<TemplateTone, string> = {
  primary: "bg-primary/10 text-primary",
  success: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300",
  warning: "bg-amber-500/10 text-amber-700 dark:text-amber-300",
  secondary: "bg-secondary/10 text-secondary-foreground",
};

export interface TemplatesPickerProps {
  onSelect: (campaign: Partial<Campaign>) => void;
}

export function TemplatesPicker({ onSelect }: TemplatesPickerProps) {
  const [category, setCategory] = useState<TemplateCategory | "All">("All");

  const filtered = useMemo(
    () => (category === "All" ? WIZARD_TEMPLATES : WIZARD_TEMPLATES.filter((t) => t.category === category)),
    [category]
  );

  return (
    <div className="space-y-4">
      <ToggleGroup
        type="single"
        size="sm"
        value={category}
        onValueChange={(v) => v && setCategory(v as TemplateCategory | "All")}
        className="flex flex-wrap justify-start"
      >
        {WIZARD_TEMPLATE_CATEGORIES.map((cat) => (
          <ToggleGroupItem key={cat} value={cat} className="text-xs">
            {cat}
          </ToggleGroupItem>
        ))}
      </ToggleGroup>

      <div className="grid gap-4 md:grid-cols-2">
        {filtered.map((template) => (
          <TemplateCard key={template.id} template={template} onSelect={onSelect} />
        ))}
      </div>
    </div>
  );
}

function TemplateCard({
  template,
  onSelect,
}: {
  template: CampaignTemplate;
  onSelect: (campaign: Partial<Campaign>) => void;
}) {
  return (
    <Card className="flex flex-col">
      <CardHeader>
        <div className="flex items-start gap-3">
          <span
            className={`flex h-10 w-10 items-center justify-center rounded-md text-xl ${TONE_CLASS[template.tone]}`}
            aria-hidden="true"
          >
            {template.icon}
          </span>
          <div className="flex-1 space-y-1">
            <div className="flex items-center justify-between gap-2">
              <CardTitle className="text-base">{template.name}</CardTitle>
              <Badge variant="outline" className="text-[10px] uppercase tracking-wide">
                {template.category}
              </Badge>
            </div>
            <CardDescription>{template.description}</CardDescription>
          </div>
        </div>
      </CardHeader>
      <CardContent className="flex flex-1 flex-col justify-between gap-4">
        <div className="space-y-2">
          <p className="text-sm text-muted-foreground">{template.preview}</p>
          <div className="flex flex-wrap gap-1.5">
            {template.tags.map((tag) => (
              <Badge key={tag} variant="secondary">
                {tag}
              </Badge>
            ))}
          </div>
        </div>
        <Button type="button" onClick={() => onSelect(template.createCampaign())}>
          Use this template
        </Button>
      </CardContent>
    </Card>
  );
}
