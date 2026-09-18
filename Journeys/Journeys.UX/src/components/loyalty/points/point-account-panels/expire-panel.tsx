"use client";

/**
 * ExpirePanel — expire points from a point account type.
 *
 * The API has no expire route: expiry posts the same withdrawal as Spend and
 * is distinguished by the audit action the server action attaches. The amount
 * can be absolute or a percentage of `currentBalance`.
 */

import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { toast } from "react-hot-toast";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription } from "@/components/ui/alert";
import { Checkbox } from "@/components/ui/checkbox";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

import { expirePoints } from "@/services/loyalty/actions";
import { confirmPointAdjustment } from "./confirm-point-adjustment";
import { invalidatePointQueries } from "./invalidate-point-queries";

const FormSchema = z
  .object({
    amount: z.number().positive("Amount must be > 0"),
    isPercent: z.boolean(),
    comment: z.string().min(1, "Comment is required"),
  })
  .superRefine((val, ctx) => {
    if (val.isPercent && val.amount > 100) {
      ctx.addIssue({ code: "custom", path: ["amount"], message: "Percentage cannot exceed 100" });
    }
    if (!val.isPercent && val.amount > 1_000_000) {
      ctx.addIssue({ code: "custom", path: ["amount"], message: "Amount too large" });
    }
  });

type FormValues = z.infer<typeof FormSchema>;

export interface ExpirePanelProps {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  currentBalance?: number;
  onUpdated?: () => void;
}

export function ExpirePanel({
  loyaltyAccountId,
  pointAccountTypeId,
  currentBalance,
  onUpdated,
}: ExpirePanelProps) {
  const queryClient = useQueryClient();

  const form = useForm<FormValues>({
    resolver: zodResolver(FormSchema),
    defaultValues: { amount: 0, isPercent: false, comment: "" },
  });

  useEffect(() => {
    form.reset({ amount: 0, isPercent: false, comment: "" });
  }, [loyaltyAccountId, pointAccountTypeId, form]);

  const isPercent = useWatch({ control: form.control, name: "isPercent" });

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      if (!values.isPercent && currentBalance !== undefined && values.amount > currentBalance) {
        throw new Error(`Cannot expire more than the current balance (${currentBalance.toLocaleString()}).`);
      }
      const result = await expirePoints({
        loyaltyAccountId,
        pointAccountTypeId,
        amount: values.amount,
        isPercent: values.isPercent,
        currentBalance,
        comment: values.comment,
      });
      if (!result.success) throw new Error(result.error ?? "Failed to expire points");
      return result.data;
    },
    onSuccess: () => {
      invalidatePointQueries(queryClient, loyaltyAccountId);
      toast.success("Points expired");
      form.reset({ amount: 0, isPercent: false, comment: "" });
      onUpdated?.();
    },
    onError: (err) => {
      toast.error(err instanceof Error ? err.message : "Failed to expire points");
    },
  });

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((values) => {
          const amount = values.isPercent ? `${values.amount}%` : `${values.amount.toLocaleString()} pts`;
          if (!confirmPointAdjustment({ action: "Expire", amount })) return;
          mutation.mutate(values);
        })}
        className="space-y-4"
      >
        {mutation.error instanceof Error && (
          <Alert variant="destructive">
            <AlertDescription>{mutation.error.message}</AlertDescription>
          </Alert>
        )}

        <FormField
          control={form.control}
          name="isPercent"
          render={({ field }) => (
            <FormItem className="flex items-center gap-2 space-y-0">
              <FormControl>
                <Checkbox checked={field.value} onCheckedChange={(v) => field.onChange(v === true)} />
              </FormControl>
              <FormLabel className="!mt-0">Expire as a percentage of current balance</FormLabel>
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="amount"
          render={({ field }) => (
            <FormItem>
              <FormLabel>{isPercent ? "Percent to expire (%)" : "Amount to expire (pts)"}</FormLabel>
              <FormControl>
                <Input
                  type="number"
                  min={0}
                  max={isPercent ? 100 : undefined}
                  step={isPercent ? 1 : "any"}
                  value={field.value || ""}
                  onChange={(e) => field.onChange(Number.parseFloat(e.target.value) || 0)}
                />
              </FormControl>
              {currentBalance !== undefined && (
                <FormDescription>
                  Current balance: {currentBalance.toLocaleString()} pts.
                  {!isPercent ? " Absolute expiry cannot exceed this balance." : ""}
                </FormDescription>
              )}
              <FormMessage />
            </FormItem>
          )}
        />

        <FormField
          control={form.control}
          name="comment"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Comment</FormLabel>
              <FormControl>
                <Textarea rows={3} placeholder="Reason for this expiration…" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex justify-end">
          <Button type="submit" disabled={mutation.isPending} variant="destructive">
            {mutation.isPending ? "Expiring…" : "Expire points"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
