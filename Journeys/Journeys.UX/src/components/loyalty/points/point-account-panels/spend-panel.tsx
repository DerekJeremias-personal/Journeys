"use client";

/**
 * SpendPanel — withdraw (spend) points from a point account type.
 */

import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { z } from "zod";
import { toast } from "react-hot-toast";

import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Textarea } from "@/components/ui/textarea";
import { Alert, AlertDescription } from "@/components/ui/alert";
import {
  Form,
  FormControl,
  FormDescription,
  FormField,
  FormItem,
  FormLabel,
  FormMessage,
} from "@/components/ui/form";

import { withdrawPoints } from "@/services/loyalty/actions";
import { confirmPointAdjustment } from "./confirm-point-adjustment";
import { invalidatePointQueries } from "./invalidate-point-queries";

const FormSchema = z.object({
  amount: z.number().positive("Amount must be > 0").max(1_000_000, "Amount too large"),
  comment: z.string().min(1, "Comment is required"),
});

type FormValues = z.infer<typeof FormSchema>;

export interface SpendPanelProps {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  currentBalance?: number;
  onUpdated?: () => void;
}

export function SpendPanel({
  loyaltyAccountId,
  pointAccountTypeId,
  currentBalance,
  onUpdated,
}: SpendPanelProps) {
  const queryClient = useQueryClient();

  const form = useForm<FormValues>({
    resolver: zodResolver(FormSchema),
    defaultValues: { amount: 0, comment: "" },
  });

  useEffect(() => {
    form.reset({ amount: 0, comment: "" });
  }, [loyaltyAccountId, pointAccountTypeId, form]);

  const mutation = useMutation({
    mutationFn: async (values: FormValues) => {
      if (currentBalance !== undefined && values.amount > currentBalance) {
        throw new Error(`Cannot spend more than the current balance (${currentBalance.toLocaleString()}).`);
      }
      const result = await withdrawPoints({
        loyaltyAccountId,
        pointAccountTypeId,
        amount: values.amount,
        comment: values.comment,
      });
      if (!result.success) throw new Error(result.error ?? "Failed to spend points");
      return result.data;
    },
    onSuccess: () => {
      invalidatePointQueries(queryClient, loyaltyAccountId);
      toast.success("Points spent");
      form.reset({ amount: 0, comment: "" });
      onUpdated?.();
    },
    onError: (err) => {
      toast.error(err instanceof Error ? err.message : "Failed to spend points");
    },
  });

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((values) => {
          if (!confirmPointAdjustment({ action: "Spend", amount: `${values.amount.toLocaleString()} pts` }))
            return;
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
          name="amount"
          render={({ field }) => (
            <FormItem>
              <FormLabel>Amount to spend</FormLabel>
              <FormControl>
                <Input
                  type="number"
                  min={0}
                  step="any"
                  value={field.value || ""}
                  onChange={(e) => field.onChange(Number.parseFloat(e.target.value) || 0)}
                />
              </FormControl>
              {currentBalance !== undefined && (
                <FormDescription>Current balance: {currentBalance.toLocaleString()} pts.</FormDescription>
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
                <Textarea rows={3} placeholder="Reason for this withdrawal…" {...field} />
              </FormControl>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex justify-end">
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? "Spending…" : "Spend points"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
