#!/bin/bash
# CI guard: storefront-core packages must have ZERO PolarSharp.* dependencies
# (other than PolarSharp.BaseEntities + other storefront-core packages).
# Per Case Study 01: Lift-and-Shift Architecture for Composable .NET SDKs.

set -e

# Storefront-CORE packages (lift-safe; the `.Polar.*` packages are excluded — they're bridges that legitimately depend on PolarSharp).
STOREFRONT_CORE_PACKAGES=$(find src -type d -name "PolarSharp.EcommerceStorefronts*" \
  | grep -v "PolarSharp.EcommerceStorefronts.Polar" \
  | sort)

ALLOWED_POLARSHARP_DEPS=(
  "PolarSharp.BaseEntities"
  "PolarSharp.EcommerceStorefronts"
  "PolarSharp.EcommerceStorefronts.Abstractions"
  "PolarSharp.EcommerceStorefronts.Pipelines.OrderProcessing"
  "PolarSharp.EcommerceStorefronts.Pipelines.SubscriptionBilling"
  "PolarSharp.EcommerceStorefronts.Pipelines.RefundProcessing"
  "PolarSharp.EcommerceStorefronts.Search"
  "PolarSharp.EcommerceStorefronts.Search.MeiliSearch"
  "PolarSharp.EcommerceStorefronts.Shipping"
  "PolarSharp.EcommerceStorefronts.Shipping.Shippo"
  "PolarSharp.EcommerceStorefronts.Shipping.EasyPost"
  "PolarSharp.EcommerceStorefronts.Tax"
  "PolarSharp.EcommerceStorefronts.Tax.TaxJar"
  "PolarSharp.EcommerceStorefronts.SEO"
  "PolarSharp.EcommerceStorefronts.SEO.AMP"
  "PolarSharp.EcommerceStorefronts.SEO.Images.Cloudflare"
  "PolarSharp.EcommerceStorefronts.SEO.Images.Imgix"
  "PolarSharp.EcommerceStorefronts.GuestSessions"
  "PolarSharp.EcommerceStorefronts.AspNetCore"
  "PolarSharp.EcommerceStorefronts.Blazor"
  "PolarSharp.EcommerceStorefronts.MAUI"
  "PolarSharp.EcommerceStorefronts.Themes"
  "PolarSharp.EcommerceStorefronts.Themes.Modern"
  "PolarSharp.EcommerceStorefronts.Themes.Classic"
  "PolarSharp.EcommerceStorefronts.Themes.Minimal"
  "PolarSharp.EcommerceStorefronts.WebComponents"
  "PolarSharp.EcommerceStorefronts.WebComponents.SignalRHub"
  "PolarSharp.EcommerceStorefronts.WebComponents.Admin"
  "PolarSharp.EcommerceStorefronts.GraphQL"
)

violations_found=0

for pkg_dir in $STOREFRONT_CORE_PACKAGES; do
  pkg_name=$(basename "$pkg_dir")
  echo "Checking $pkg_name for forbidden PolarSharp.* dependencies..."
  forbidden=$(dotnet list "$pkg_dir" package --include-transitive 2>/dev/null \
    | grep -E "^\s+>\s+PolarSharp\." \
    | awk '{print $2}' \
    | sort -u || true)

  for dep in $forbidden; do
    if [[ ! " ${ALLOWED_POLARSHARP_DEPS[*]} " =~ " ${dep} " ]]; then
      echo "  FORBIDDEN: $pkg_name -> $dep"
      violations_found=$((violations_found + 1))
    fi
  done
done

if [ "$violations_found" -gt 0 ]; then
  echo ""
  echo "FAIL: $violations_found forbidden-dependency violation(s) found in storefront-core packages."
  echo "Per Case Study 01 lift-and-shift contract: storefront-core may only reference"
  echo "PolarSharp.BaseEntities + other storefront-core packages. Polar integrations"
  echo "must live in PolarSharp.EcommerceStorefronts.Polar.* bridges, not in core."
  exit 1
fi

echo ""
echo "PASS: All storefront-core packages are lift-safe (zero PolarSharp.* coupling outside BaseEntities + storefront-core family)."
