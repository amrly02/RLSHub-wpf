-- CarSwap Marketplace Configuration
-- Replace these values with your Supabase project details

return {
  SUPABASE_URL = "https://vehfqsnxuzpwcotnmtga.supabase.co",
  SUPABASE_ANON_KEY = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6InZlaGZxc254dXpwd2NvdG5tdGdhIiwicm9sZSI6ImFub24iLCJpYXQiOjE3Njk2ODA4NjgsImV4cCI6MjA4NTI1Njg2OH0.RxExzubDweXpNjO5DWvf1fX2oY02mY7OOQxY3eOgZ5A",

  LISTING_DURATION_DAYS = 7,
  MAX_ACTIVE_LISTINGS = 10,
  MIN_PRICE = 100,
  MAX_PRICE = 50000000,

  ENABLE_THUMBNAILS = true,
  MAX_THUMBNAIL_SIZE = 50000,

  CACHE_DURATION = 60,
  REQUEST_TIMEOUT = 10,

  ENABLE_MESSAGES = true,
  ENABLE_RATINGS = true,
  ENABLE_WATCHLIST = true
}
