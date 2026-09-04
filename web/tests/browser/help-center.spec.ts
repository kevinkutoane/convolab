import { test, expect } from '@playwright/test';

test.describe('Help Center Page', () => {
  test.beforeEach(async ({ page }) => {
    // Navigate to the Help Center page.
    await page.goto('/help');
  });

  test('should display the header and correct active tab', async ({ page }) => {
    await expect(page.locator('.help-center-header h1')).toHaveText('ConvoLab Help Center');
    
    // Check that Getting Started is the active tab
    const activeTab = page.locator('.help-tabs button.active');
    await expect(activeTab).toHaveText('Getting Started');
  });

  test('should allow switching tabs and viewing content', async ({ page }) => {
    // Click on Screen-by-Screen Guide tab
    await page.locator('.help-tabs button', { hasText: 'Screen-by-Screen Guide' }).click();
    
    // Check that we see the screen cards
    await expect(page.locator('.help-card').first()).toBeVisible();
    await expect(page.locator('h3', { hasText: 'Platform Dashboard' })).toBeVisible();
  });

  test('should filter screens using the search bar', async ({ page }) => {
    // Go to Screen-by-Screen guide tab
    await page.locator('.help-tabs button', { hasText: 'Screen-by-Screen Guide' }).click();
    
    const searchInput = page.locator('.help-search-container input');
    
    // Type into search bar
    await searchInput.fill('drag-and-drop tool');
    
    // Should filter down to Workflow Designer
    await expect(page.locator('h3', { hasText: 'Workflow Designer' })).toBeVisible();
    await expect(page.locator('h3', { hasText: 'Platform Dashboard' })).not.toBeVisible();
  });

  test('should automatically switch to screens tab when typing in search', async ({ page }) => {
    // Initially on Getting Started
    await expect(page.locator('.help-tabs button.active')).toHaveText('Getting Started');
    
    const searchInput = page.locator('.help-search-container input');
    await searchInput.fill('simulator');
    
    // Should automatically switch to screens tab
    await expect(page.locator('.help-tabs button.active')).toHaveText('Screen-by-Screen Guide');
    await expect(page.locator('h3', { hasText: 'Conversation Simulator' })).toBeVisible();
  });

  test('should expand a screen card to show details', async ({ page }) => {
    await page.locator('.help-tabs button', { hasText: 'Screen-by-Screen Guide' }).click();
    
    // Find the dashboard card
    const dashboardCard = page.locator('.help-card').filter({ hasText: 'Platform Dashboard' });
    
    // Details should be hidden initially
    await expect(dashboardCard.locator('.help-card-details')).not.toBeVisible();
    
    // Click to expand
    await dashboardCard.click();
    
    // Details should be visible
    await expect(dashboardCard.locator('.help-card-details')).toBeVisible();
    await expect(dashboardCard.locator('h4', { hasText: 'Required Role' })).toBeVisible();
    
    // Click the X button to close
    await dashboardCard.locator('button.icon-button').click();
    
    // Details should be hidden again
    await expect(dashboardCard.locator('.help-card-details')).not.toBeVisible();
  });
});
