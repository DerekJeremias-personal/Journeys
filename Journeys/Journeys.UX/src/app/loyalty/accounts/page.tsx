import { queryData } from "@/services/loyalty/actions";
import { loyaltyScreenModel } from "@/lib/loyalty-model";
import { attributeNamesFromRows } from "@/services/loyalty/parse-list";

function formatFetchError(error?: string): string {
  const e = error ?? "";
  if (/401|403|unauthorized|forbidden|nope/i.test(e)) {
    return "not authorized / check tenant or key";
  }
  return e || "Unknown error";
}

export default async function AccountsPage() {
  const screen = loyaltyScreenModel("accounts");
  const result = await queryData(screen.schemaName!);

  const attributes =
    result.success && result.data && result.data.length > 0
      ? attributeNamesFromRows(result.data)
      : [{ name: "id" }, { name: "name" }];

  return (
    <>
      <h1>Accounts</h1>
      {!result.success ? (
        <p className="error">{formatFetchError(result.error)}</p>
      ) : result.data!.length === 0 ? (
        <p className="empty">No loyalty accounts found.</p>
      ) : (
        <table>
          <thead>
            <tr>
              {attributes.map((attr, index) => (
                <th key={attr.name ?? index}>{attr.name}</th>
              ))}
            </tr>
          </thead>
          <tbody>
            {result.data!.map((row, rowIndex) => (
              <tr key={String(row.id ?? rowIndex)}>
                {attributes.map((attr, colIndex) => (
                  <td key={attr.name ?? colIndex}>{String(row[attr.name ?? ""] ?? "")}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      )}
    </>
  );
}
