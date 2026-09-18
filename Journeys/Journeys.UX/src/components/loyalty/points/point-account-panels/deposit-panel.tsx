"use client";

/**
 * DepositPanel — react-hook-form + Zod form to deposit points into a point
 * account type. The amount and a comment are the only inputs; the deposit has
 * no expiration date.
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

import { depositPoints } from "@/services/loyalty/actions";
import { confirmPointAdjustment } from "./confirm-point-adjustment";
import { invalidatePointQueries } from "./invalidate-point-queries";

const FormSchema = z.object({
  amount: z.number().positive("Amount must be > 0").max(1_000_000, "Amount too large"),
  comment: z.string().min(1, "Comment is required"),
});

type FormValues = z.infer<typeof FormSchema>;

export interface DepositPanelProps {
  loyaltyAccountId: string;
  pointAccountTypeId: string;
  onUpdated?: () => void;
}

export function DepositPanel({ loyaltyAccountId, pointAccountTypeId, onUpdated }: DepositPanelProps) {
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
      const result = await depositPoints({
        loyaltyAccountId,
        pointAccountTypeId,
        amount: values.amount,
        comment: values.comment,
      });
      if (!result.success) throw new Error(result.error ?? "Failed to deposit points");
      return result.data;
    },
    onSuccess: () => {
      invalidatePointQueries(queryClient, loyaltyAccountId);
      toast.success("Points deposited");
      form.reset({ amount: 0, comment: "" });
      onUpdated?.();
    },
    onError: (err) => {
      toast.error(err instanceof Error ? err.message : "Failed to deposit points");
    },
  });

  return (
    <Form {...form}>
      <form
        onSubmit={form.handleSubmit((values) => {
          if (!confirmPointAdjustment({ action: "Deposit", amount: `${values.amount.toLocaleString()} pts` }))
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
              <FormLabel>Amount to deposit</FormLabel>
              <FormControl>
                <Input
                  type="number"
                  min={0}
                  max={1_000_000}
                  step={1}
                  value={field.value || ""}
                  onChange={(e) => field.onChange(Number.parseFloat(e.target.value) || 0)}
                />
              </FormControl>
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
                <Textarea rows={3} placeholder="Reason for this deposit…" {...field} />
              </FormControl>
              <FormDescription>Recorded with the adjustment.</FormDescription>
              <FormMessage />
            </FormItem>
          )}
        />

        <div className="flex justify-end">
          <Button type="submit" disabled={mutation.isPending}>
            {mutation.isPending ? "Depositing…" : "Deposit points"}
          </Button>
        </div>
      </form>
    </Form>
  );
}
